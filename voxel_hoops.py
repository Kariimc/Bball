# ==============================================================================
#                               VOXEL_HOOPS.PY
# ==============================================================================
# Real-time isometric voxel basketball -- THE GAME (source of truth).
#
# Single source of truth, no duplicated code:
#   * Vector3 + math      -> imported from the simulation engine.
#   * Court geometry/rules -> imported from court_rules.py.
#   * Teams + ratings      -> loaded from the shared roster registry (2005,
#                             modern, and URL-imported teams) via arcade_adapter.
#
# What this build adds over the original prototype:
#   * Two-way scoring (both teams attack their own hoop) with 2 vs 3 pointers.
#   * A real game: quarter clock, 4 quarters + overtime, halftime, final score.
#   * AI that chases loose balls, drives to its hoop, and shoots/scores.
#   * Possession reset after makes, out-of-bounds turnovers, restart.
#   * Expanded Steam achievements + rich presence.
#
# Run:  pip install pygame && python voxel_hoops.py --home BOS --away DEN
#       python voxel_hoops.py --import-url https://example.com/team.json
# ==============================================================================

import sys
import math
import random
import argparse
from enum import Enum

import pygame

# --- single source of truth imports ------------------------------------------
from nba_comprehensive_game_engine import Vector3
from arcade_adapter import get_arcade_matchup
from court_rules import (
    COURT_WIDTH, COURT_LENGTH, RIM_HEIGHT, SCORE_RADIUS, DUNK_RANGE, SHOOT_RANGE,
    HOME_HOOP, AWAY_HOOP, distance, target_hoop, point_value, is_scoring_position,
    in_bounds, format_clock,
)

# ==============================================================================
# 1. STEAM API INTEGRATION LAYER
# ==============================================================================
class SteamManager:
    """Native integration hooks with the Steamworks SDK, with a fallback mode."""
    def __init__(self):
        self.initialized = False
        self.achievements_unlocked = set()
        self.rich_presence = "Main Menu"
        self.init_steam()

    def init_steam(self):
        try:
            # Future hooks for steamworks.py / pySteamWorks bindings go here.
            print("[STEAM] Initializing Steamworks API Stub... Success.")
            self.initialized = True
            self.set_rich_presence("Warmups", "Practicing in the Voxel Gym")
        except Exception as e:
            print(f"[STEAM] Running in DRM-Free Standalone Mode: {e}")
            self.initialized = False

    def set_rich_presence(self, status, detail=""):
        self.rich_presence = f"{status} - {detail}"
        if self.initialized:
            print(f"[STEAM RPC] Updated Status: {self.rich_presence}")

    def unlock_achievement(self, api_name):
        if api_name not in self.achievements_unlocked:
            self.achievements_unlocked.add(api_name)
            print(f"[STEAM ACHIEVEMENT] UNLOCKED: {api_name}")

# ==============================================================================
# 2. CONSTANTS & ENUMS  (court geometry comes from court_rules)
# ==============================================================================
FPS = 60
SCREEN_WIDTH = 1280
SCREEN_HEIGHT = 720
GAME_TITLE = "Voxel Hoops (Steam Edition)"

GRID_SCALE = 5

QUARTER_SECONDS = 120.0   # arcade-length quarters
OVERTIME_SECONDS = 60.0
NUM_QUARTERS = 4

COLOR_BG = (18, 18, 24)
COLOR_COURT = (222, 137, 73)
COLOR_LINES = (245, 245, 250)
COLOR_BALL = (242, 108, 31)

class PlayerState(Enum):
    IDLE = 0
    DRIBBLING = 1
    SHOOTING = 2
    DUNK_ANIMATION = 3
    STUNNED = 4

# Layered voxel arrays from feet to head.
VOXEL_PLAYER_MESH = {
    "head":  [ (0,0,6), (0,1,6), (1,0,6), (1,1,6) ],
    "torso": [ (0,0,3), (0,1,3), (1,0,3), (1,1,3), (0,0,4), (0,1,4), (1,0,4), (1,1,4), (0,0,5), (0,1,5), (1,0,5), (1,1,5) ],
    "legs":  [ (0,0,1), (0,1,1), (1,0,1), (1,1,1), (0,0,2), (0,1,2), (1,0,2), (1,1,2) ]
}

# ==============================================================================
# 3. PROJECTION  (Vector3 imported from engine -- no local duplicate)
# ==============================================================================
def iso_project(x, y, z):
    """Transforms 3D world vectors into 2D isometric pixel locations."""
    angle = math.radians(30)
    iso_x = (x - y) * math.cos(angle)
    iso_y = (x + y) * math.sin(angle) - z
    screen_x = int(iso_x * GRID_SCALE) + (SCREEN_WIDTH // 2)
    screen_y = int(iso_y * GRID_SCALE) + (SCREEN_HEIGHT // 2) - 50
    return screen_x, screen_y

# ==============================================================================
# 4. CORE ENTITIES
# ==============================================================================
class Ball:
    def __init__(self, x, y, z):
        self.pos = Vector3(x, y, z)
        self.vel = Vector3(0, 0, 0)
        self.radius = 1.2
        self.is_held = False
        self.handler = None

    def update(self):
        if self.is_held and self.handler:
            self.pos.x = self.handler.pos.x + 1.5
            self.pos.y = self.handler.pos.y
            if self.handler.state == PlayerState.DRIBBLING:
                self.pos.z = self.handler.pos.z + 3.0 + abs(math.sin(pygame.time.get_ticks() * 0.015) * 2.5)
            else:
                self.pos.z = self.handler.pos.z + 4.0
            self.vel.x, self.vel.y, self.vel.z = 0, 0, 0
        else:
            if self.pos.z > 0:
                self.vel.z -= 0.35
            self.pos.x += self.vel.x
            self.pos.y += self.vel.y
            self.pos.z += self.vel.z
            # Elastic floor bounce.
            if self.pos.z <= 0:
                self.pos.z = 0
                self.vel.z = -self.vel.z * 0.65
                self.vel.x *= 0.95
                self.vel.y *= 0.95

    def draw(self, surface):
        sh_x, sh_y = iso_project(self.pos.x, self.pos.y, 0)
        pygame.draw.ellipse(surface, (25, 25, 30), (sh_x - 7, sh_y - 3, 14, 6))
        sx, sy = iso_project(self.pos.x, self.pos.y, self.pos.z)
        pygame.draw.circle(surface, COLOR_BALL, (sx, sy), int(self.radius * GRID_SCALE))

class Player:
    _font = None  # lazily-created name-tag font

    def __init__(self, x, y, team, stats, name="", is_user=False):
        self.pos = Vector3(x, y, 0.0)
        self.vel = Vector3(0, 0, 0)
        self.team = team
        self.name = name
        self.is_user = is_user
        self.state = PlayerState.IDLE
        self.state_timer = 0
        self.shoot_cooldown = 0
        self.stats = {
            "speed": stats["speed"],
            "vertical": stats["vertical"],
            "steal": stats["steal"],
            "shooting": stats["shooting"],
        }
        self.jersey_color = (46, 204, 113) if team == "home" else (231, 76, 60)
        self.skin_color = (253, 227, 167) if is_user else (245, 183, 177)

    def move(self, dx, dy):
        if self.state in (PlayerState.STUNNED, PlayerState.DUNK_ANIMATION):
            return
        speed_modifier = 0.6 + (self.stats["speed"] / 20.0)
        self.vel.x = dx * speed_modifier
        self.vel.y = dy * speed_modifier
        if dx != 0 or dy != 0:
            if self.state != PlayerState.SHOOTING:
                self.state = PlayerState.DRIBBLING if self.pos.z == 0 else self.state
        else:
            if self.state == PlayerState.DRIBBLING:
                self.state = PlayerState.IDLE

    def update(self, ball=None):
        if self.shoot_cooldown > 0:
            self.shoot_cooldown -= 1
        if self.state_timer > 0:
            self.state_timer -= 1
            if self.state_timer == 0:
                self.state = PlayerState.IDLE

        if self.state == PlayerState.DUNK_ANIMATION:
            target_x = target_hoop(self.team)[0]
            self.pos.x += (target_x - self.pos.x) * 0.15
            self.pos.z = math.sin((self.state_timer / 30.0) * math.pi) * (4.0 + (self.stats["vertical"] / 2.0))
        else:
            self.pos.x += self.vel.x
            self.pos.y += self.vel.y

        self.pos.x = max(0, min(self.pos.x, COURT_LENGTH))
        self.pos.y = max(0, min(self.pos.y, COURT_WIDTH))

    def draw(self, surface):
        sh_x, sh_y = iso_project(self.pos.x, self.pos.y, 0)
        pygame.draw.ellipse(surface, (15, 15, 20), (sh_x - 14, sh_y - 7, 28, 14))

        for segment, voxels in VOXEL_PLAYER_MESH.items():
            for vx, vy, vz in voxels:
                world_x = self.pos.x + (vx * 0.5)
                world_y = self.pos.y + (vy * 0.5)
                world_z = self.pos.z + (vz * 0.8)
                sx, sy = iso_project(world_x, world_y, world_z)
                if segment == "head": col = self.skin_color
                elif segment == "torso": col = self.jersey_color
                else: col = (44, 62, 80)
                sz = int(0.5 * GRID_SCALE * 2)
                pygame.draw.rect(surface, col, (sx - sz//2, sy - sz//2, sz, sz))

        # Real names come from the roster source of truth -> show them.
        if self.name:
            if Player._font is None:
                Player._font = pygame.font.SysFont("Courier New", 12, bold=True)
            hx, hy = iso_project(self.pos.x, self.pos.y, self.pos.z + 7)
            tag = Player._font.render(self.name, True, (240, 240, 245))
            surface.blit(tag, (hx - tag.get_width() // 2, hy - 18))

# ==============================================================================
# 5. ENVIRONMENT ARCHITECTURE
# ==============================================================================
class BasketballCourt:
    def draw(self, surface):
        p1 = iso_project(0, 0, 0)
        p2 = iso_project(COURT_LENGTH, 0, 0)
        p3 = iso_project(COURT_LENGTH, COURT_WIDTH, 0)
        p4 = iso_project(0, COURT_WIDTH, 0)
        pygame.draw.polygon(surface, COLOR_COURT, [p1, p2, p3, p4])
        pygame.draw.polygon(surface, COLOR_LINES, [p1, p2, p3, p4], 3)

        mid_top = iso_project(COURT_LENGTH // 2, 0, 0)
        mid_bot = iso_project(COURT_LENGTH // 2, COURT_WIDTH, 0)
        pygame.draw.line(surface, COLOR_LINES, mid_top, mid_bot, 3)

        center_x, center_y = iso_project(COURT_LENGTH // 2, COURT_WIDTH // 2, 0)
        pygame.draw.ellipse(surface, COLOR_LINES, (center_x - 50, center_y - 25, 100, 50), 3)

        self._draw_hoop_voxel_cluster(surface, AWAY_HOOP[0], AWAY_HOOP[1], is_left=True)
        self._draw_hoop_voxel_cluster(surface, HOME_HOOP[0], HOME_HOOP[1], is_left=False)

    def _draw_hoop_voxel_cluster(self, surface, x, y, is_left):
        p_bot = iso_project(x, y, 0)
        p_top = iso_project(x, y, 16)
        pygame.draw.line(surface, (149, 165, 166), p_bot, p_top, 5)

        offset = 2.5 if is_left else -2.5
        bb1 = iso_project(x + offset, y - 5, 14)
        bb2 = iso_project(x + offset, y + 5, 14)
        bb3 = iso_project(x + offset, y + 5, 20)
        bb4 = iso_project(x + offset, y - 5, 20)
        pygame.draw.polygon(surface, (236, 240, 241), [bb1, bb2, bb3, bb4])
        pygame.draw.polygon(surface, (231, 76, 60), [bb1, bb2, bb3, bb4], 2)

        rim_x = x + (4.5 if is_left else -4.5)
        rx, ry = iso_project(rim_x, y, RIM_HEIGHT)
        pygame.draw.ellipse(surface, (211, 84, 0), (rx - 10, ry - 5, 20, 10), 3)

# ==============================================================================
# 6. MAIN ENGINE CORE PIPELINE
# ==============================================================================
class GameEngine:
    def __init__(self, home_key="BOS", away_key="DEN"):
        pygame.init()
        self.screen = pygame.display.set_mode((SCREEN_WIDTH, SCREEN_HEIGHT))
        pygame.display.set_caption(GAME_TITLE)
        self.clock = pygame.time.Clock()
        self.is_running = True

        self.steam = SteamManager()
        self.court = BasketballCourt()
        self.home_key = home_key
        self.away_key = away_key

        self.font_hud = pygame.font.SysFont("Courier New", 16, bold=True)
        self.font_big = pygame.font.SysFont("Courier New", 48, bold=True)

        self._setup_match()

    # -- match setup / reset ---------------------------------------------------
    def _setup_match(self):
        home_name, home_players, away_name, away_players = get_arcade_matchup(self.home_key, self.away_key)
        self.home_name, self.away_name = home_name, away_name

        star, opp1, opp2 = home_players[0], away_players[0], away_players[1]
        self.user_player = Player(20, COURT_WIDTH // 2, "home", star["stats"], name=star["name"], is_user=True)
        self.entities = [
            self.user_player,
            Player(70, COURT_WIDTH // 3, "away", opp1["stats"], name=opp1["name"]),
            Player(72, (COURT_WIDTH // 3) * 2, "away", opp2["stats"], name=opp2["name"]),
        ]
        self.ball = Ball(COURT_LENGTH // 2, COURT_WIDTH // 2, 25.0)

        self.score_home = 0
        self.score_away = 0
        self.quarter = 1
        self.game_clock = QUARTER_SECONDS
        self.game_over = False
        self.pending_points = 2
        self.max_home_deficit = 0
        self.banner = ""
        self.banner_timer = 0
        self.steam.set_rich_presence("Tip-Off", f"{self.home_name} vs {self.away_name}")

    # -- input -----------------------------------------------------------------
    def process_input(self):
        dx, dy = 0, 0
        keys = pygame.key.get_pressed()
        if keys[pygame.K_w] or keys[pygame.K_UP]:    dy = -1
        if keys[pygame.K_s] or keys[pygame.K_DOWN]:  dy = 1
        if keys[pygame.K_a] or keys[pygame.K_LEFT]:  dx = -1
        if keys[pygame.K_d] or keys[pygame.K_RIGHT]: dx = 1
        if not self.game_over:
            self.user_player.move(dx, dy)

        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                self.is_running = False
            elif event.type == pygame.KEYDOWN:
                if event.key == pygame.K_ESCAPE:
                    self.is_running = False
                elif event.key == pygame.K_r and self.game_over:
                    self._setup_match()
                elif not self.game_over and event.key == pygame.K_SPACE:
                    self._execute_context_action()
                elif not self.game_over and event.key == pygame.K_e:
                    self._execute_steal_attempt()

    # -- shared shot logic (user + AI) ----------------------------------------
    def _launch_shot(self, shooter):
        hx, hy = target_hoop(shooter.team)
        dist = distance(shooter.pos.x, shooter.pos.y, hx, hy)
        self.pending_points = point_value(shooter.pos.x, shooter.pos.y, shooter.team)

        shooter.state = PlayerState.SHOOTING
        shooter.state_timer = 15
        shooter.shoot_cooldown = 40
        self.ball.is_held = False
        self.ball.handler = None

        variance = max(0.06, (10 - shooter.stats["shooting"]) * 0.02)
        self.ball.vel.x = (hx - shooter.pos.x) * 0.045 + random.uniform(-variance, variance)
        self.ball.vel.y = (hy - shooter.pos.y) * 0.045 + random.uniform(-variance, variance)
        self.ball.vel.z = 7.5 + (dist * 0.05)

    def _start_dunk(self, shooter):
        shooter.state = PlayerState.DUNK_ANIMATION
        shooter.state_timer = 30
        shooter.shoot_cooldown = 40
        self.pending_points = 2
        if shooter.is_user:
            self.steam.unlock_achievement("FIRST_DUNK")
            self.steam.set_rich_presence("In Game", "Executing a Monster Dunk!")

    def _execute_context_action(self):
        if self.ball.is_held and self.ball.handler == self.user_player:
            hx, hy = target_hoop("home")
            dist = distance(self.user_player.pos.x, self.user_player.pos.y, hx, hy)
            if dist < DUNK_RANGE and self.user_player.stats["vertical"] >= 7:
                self._start_dunk(self.user_player)
            else:
                self._launch_shot(self.user_player)
                self.steam.set_rich_presence("In Game", "Taking a Jump Shot")
        else:
            d = distance(self.user_player.pos.x, self.user_player.pos.y, self.ball.pos.x, self.ball.pos.y)
            if d < 6.0 and self.ball.pos.z < 5.0:
                self._give_ball(self.user_player)

    def _execute_steal_attempt(self):
        if not self.ball.is_held or self.ball.handler == self.user_player:
            return
        target = self.ball.handler
        if distance(self.user_player.pos.x, self.user_player.pos.y, target.pos.x, target.pos.y) < 7.0:
            if random.random() < 0.2 + (self.user_player.stats["steal"] * 0.05):
                target.state = PlayerState.STUNNED
                target.state_timer = 45
                self._give_ball(self.user_player)
                self.steam.unlock_achievement("CLEAN_STEAL")

    def _give_ball(self, player):
        self.ball.is_held = True
        self.ball.handler = player
        player.state = PlayerState.DRIBBLING

    # -- AI --------------------------------------------------------------------
    def _process_ai_behavior_trees(self):
        for entity in self.entities:
            if entity.is_user or entity.state in (PlayerState.STUNNED, PlayerState.DUNK_ANIMATION):
                continue

            if not self.ball.is_held:
                # Chase the loose ball.
                self._steer(entity, self.ball.pos.x, self.ball.pos.y)
            elif self.ball.handler is entity:
                hx, hy = target_hoop(entity.team)
                dist = distance(entity.pos.x, entity.pos.y, hx, hy)
                if dist < DUNK_RANGE and entity.stats["vertical"] >= 7 and random.random() < 0.04:
                    self._start_dunk(entity)
                elif dist < SHOOT_RANGE and entity.shoot_cooldown == 0 and random.random() < 0.03:
                    self._launch_shot(entity)
                else:
                    self._steer(entity, hx, hy)        # drive to the rim
            elif self.ball.handler is self.user_player:
                # Defend: shadow the user, gamble for a steal when close.
                self._steer(entity, self.user_player.pos.x, self.user_player.pos.y)
                if distance(entity.pos.x, entity.pos.y, self.user_player.pos.x, self.user_player.pos.y) < 4.0:
                    if random.random() < 0.01 + entity.stats["steal"] * 0.004:
                        self.user_player.state = PlayerState.STUNNED
                        self.user_player.state_timer = 30
                        self._give_ball(entity)
            else:
                # Teammate has it: spread toward our hoop.
                self._steer(entity, target_hoop(entity.team)[0], entity.pos.y)

    def _steer(self, entity, tx, ty):
        dx = 1 if tx > entity.pos.x + 0.5 else (-1 if tx < entity.pos.x - 0.5 else 0)
        dy = 1 if ty > entity.pos.y + 0.5 else (-1 if ty < entity.pos.y - 0.5 else 0)
        entity.move(dx, dy)

    # -- update ----------------------------------------------------------------
    def update(self, dt):
        if self.game_over:
            return

        self.game_clock -= dt
        if self.game_clock <= 0:
            self._end_period()
            return

        if self.banner_timer > 0:
            self.banner_timer -= 1
            if self.banner_timer == 0:
                self.banner = ""

        self._process_ai_behavior_trees()
        for entity in self.entities:
            entity.update(self.ball)
        self.ball.update()

        # Dunk finish scores for the dunker's team.
        for e in self.entities:
            if e.state == PlayerState.DUNK_ANIMATION and e.state_timer == 1:
                self._award(e.team, 2, e, dunk=True)

        # Made jump shots at either hoop.
        if not self.ball.is_held and self.ball.vel.z < 0:
            for team, hoop in (("home", HOME_HOOP), ("away", AWAY_HOOP)):
                if is_scoring_position(self.ball.pos.x, self.ball.pos.y, self.ball.pos.z, hoop):
                    self._award(team, self.pending_points)
                    break

        # Out of bounds -> turnover (loose ball back at center).
        if not self.ball.is_held and not in_bounds(self.ball.pos.x, self.ball.pos.y):
            self._reset_ball_center()

        # Loose-ball pickup for anyone in range.
        if not self.ball.is_held:
            for entity in self.entities:
                if entity.state == PlayerState.STUNNED:
                    continue
                if distance(entity.pos.x, entity.pos.y, self.ball.pos.x, self.ball.pos.y) < 3.5 and self.ball.pos.z < 4.0:
                    self._give_ball(entity)
                    break

        # Track comeback potential (home's worst deficit).
        self.max_home_deficit = max(self.max_home_deficit, self.score_away - self.score_home)

    def _award(self, team, points, scorer=None, dunk=False):
        if team == "home":
            self.score_home += points
        else:
            self.score_away += points

        name = scorer.name if scorer else team
        self.banner = f"{name} +{points}" + ("  DUNK!" if dunk else "")
        self.banner_timer = 90

        if self.score_home + self.score_away == points:
            self.steam.unlock_achievement("FIRST_BASKET")
        if self.game_clock <= 3.0:
            self.steam.unlock_achievement("BUZZER_BEATER")
        self.steam.set_rich_presence("Score Update", f"{self.home_name} {self.score_home} - {self.score_away} {self.away_name}")
        self._reset_ball_center()

    def _reset_ball_center(self):
        self.ball.is_held = False
        self.ball.handler = None
        self.ball.pos = Vector3(COURT_LENGTH / 2, COURT_WIDTH / 2, 8.0)
        self.ball.vel = Vector3(0, 0, 0)
        self.pending_points = 2

    def _end_period(self):
        if self.quarter < NUM_QUARTERS:
            self.quarter += 1
            self.game_clock = QUARTER_SECONDS
            self.banner, self.banner_timer = f"END OF Q{self.quarter - 1}", 120
            self._reset_ball_center()
        elif self.score_home == self.score_away:
            self.quarter += 1
            self.game_clock = OVERTIME_SECONDS
            self.banner, self.banner_timer = "OVERTIME!", 120
            self._reset_ball_center()
        else:
            self.game_over = True
            winner = self.home_name if self.score_home > self.score_away else self.away_name
            if self.score_home > self.score_away and self.max_home_deficit >= 10:
                self.steam.unlock_achievement("COMEBACK_KID")
            self.steam.set_rich_presence("Post-Game", f"{winner} win")

    # -- render ----------------------------------------------------------------
    def render(self):
        self.screen.fill(COLOR_BG)
        self.court.draw(self.screen)

        render_queue = [(e.pos.y, e) for e in self.entities]
        if not self.ball.is_held:
            render_queue.append((self.ball.pos.y, self.ball))
        render_queue.sort(key=lambda element: element[0])
        for _, obj in render_queue:
            obj.draw(self.screen)
        if self.ball.is_held:
            self.ball.draw(self.screen)

        self._render_interface_overlay()
        if self.game_over:
            self._render_final()
        pygame.display.flip()

    def _render_interface_overlay(self):
        f = self.font_hud
        ctrls = f.render("WASD/Arrows: Run | Space: Catch/Shoot/Dunk | E: Strip Steal | R: Restart", True, (220, 220, 230))
        period = f"Q{self.quarter}" if self.quarter <= NUM_QUARTERS else f"OT{self.quarter - NUM_QUARTERS}"
        score = f.render(f"{self.home_name} {self.score_home}  -  {self.score_away} {self.away_name}    [{period}  {format_clock(self.game_clock)}]", True, (241, 196, 15))
        stats = f.render(f"{self.user_player.name} -> SPD {self.user_player.stats['speed']} VERT {self.user_player.stats['vertical']} STL {self.user_player.stats['steal']} SHT {self.user_player.stats['shooting']}", True, (52, 152, 219))
        state = f.render(f"STATE: {self.user_player.state.name}", True, (142, 68, 173))

        self.screen.blit(ctrls, (25, 18))
        self.screen.blit(score, (25, 42))
        self.screen.blit(stats, (25, SCREEN_HEIGHT - 55))
        self.screen.blit(state, (25, SCREEN_HEIGHT - 35))

        if self.banner:
            b = self.font_big.render(self.banner, True, (255, 255, 255))
            self.screen.blit(b, (SCREEN_WIDTH // 2 - b.get_width() // 2, 90))

    def _render_final(self):
        winner = self.home_name if self.score_home > self.score_away else self.away_name
        line1 = self.font_big.render("FINAL", True, (255, 255, 255))
        line2 = self.font_big.render(f"{self.home_name} {self.score_home} - {self.score_away} {self.away_name}", True, (241, 196, 15))
        line3 = self.font_hud.render(f"{winner} win!   Press R to rematch, ESC to quit.", True, (220, 220, 230))
        cx = SCREEN_WIDTH // 2
        self.screen.blit(line1, (cx - line1.get_width() // 2, 280))
        self.screen.blit(line2, (cx - line2.get_width() // 2, 340))
        self.screen.blit(line3, (cx - line3.get_width() // 2, 410))

    def run(self):
        while self.is_running:
            dt = self.clock.tick(FPS) / 1000.0
            self.process_input()
            self.update(dt)
            self.render()
        pygame.quit()
        sys.exit()


def main():
    parser = argparse.ArgumentParser(description="Voxel Hoops -- real-time arcade basketball")
    parser.add_argument("--home", default="BOS", help="home team key (see rosters)")
    parser.add_argument("--away", default="DEN", help="away team key")
    parser.add_argument("--import-url", help="import a team from a URL before tip-off")
    parser.add_argument("--import-key", help="register the imported team under this key")
    parser.add_argument("--allow-private", action="store_true",
                        help="permit loopback/private import URLs (trusted local dev only)")
    args = parser.parse_args()

    home_key, away_key = args.home.upper(), args.away.upper()
    if args.import_url:
        from team_import import import_team_from_url
        info = import_team_from_url(args.import_url, allow_private=args.allow_private, key=args.import_key)
        print(f"[IMPORT] Loaded {info['name']} as key {info['key']}")
        home_key = info["key"]

    GameEngine(home_key, away_key).run()


if __name__ == "__main__":
    main()
