# ==============================================================================
#                               VOXEL_HOOPS.PY
# ==============================================================================
# Real-time isometric voxel basketball -- THE GAME (source of truth).
#
# Single source of truth, no duplicated code:
#   * Vector3 + math       -> imported from the simulation engine.
#   * Court geometry/rules -> imported from court_rules.py (incl. shot model).
#   * Teams + ratings      -> loaded from the shared roster registry (2005,
#                             modern, URL-imported) via arcade_adapter.
#
# Features: full 5-on-5, ball-carrier control switching, a team-select menu,
# team AI (spacing, drive, shoot, pass, man defense, steals), a tunable shot
# model, quarter clock + OT, possession resets, out-of-bounds, Steam hooks.
#
# Run:  pip install pygame && python voxel_hoops.py
#       python voxel_hoops.py --home OKC --away MIN --no-menu
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
from rosters import all_teams
from court_rules import (
    COURT_WIDTH, COURT_LENGTH, RIM_HEIGHT, DUNK_RANGE, SHOOT_RANGE,
    HOME_HOOP, AWAY_HOOP, distance, target_hoop, point_value, is_scoring_position,
    in_bounds, format_clock, offensive_spot, make_probability,
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
            self.set_rich_presence("Main Menu", "Choosing teams")
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

QUARTER_SECONDS = 120.0
OVERTIME_SECONDS = 60.0
NUM_QUARTERS = 4

# --- pace / balance tuning knobs (tweak after a live playtest) ----------------
# Per-frame probabilities that an AI ball-handler in range fires. Lower => longer
# possessions => lower scores. Raise QUARTER_SECONDS for longer games.
AI_SHOOT_CHANCE_OPEN = 0.02
AI_SHOOT_CHANCE_CONTESTED = 0.008
AI_DUNK_CHANCE = 0.04

COLOR_BG = (18, 18, 24)
COLOR_COURT = (222, 137, 73)
COLOR_LINES = (245, 245, 250)
COLOR_BALL = (242, 108, 31)
COLOR_PICK = (241, 196, 15)   # highlight ring on the controlled player

class PlayerState(Enum):
    IDLE = 0
    DRIBBLING = 1
    SHOOTING = 2
    DUNK_ANIMATION = 3
    STUNNED = 4

VOXEL_PLAYER_MESH = {
    "head":  [ (0,0,6), (0,1,6), (1,0,6), (1,1,6) ],
    "torso": [ (0,0,3), (0,1,3), (1,0,3), (1,1,3), (0,0,4), (0,1,4), (1,0,4), (1,1,4), (0,0,5), (0,1,5), (1,0,5), (1,1,5) ],
    "legs":  [ (0,0,1), (0,1,1), (1,0,1), (1,1,1), (0,0,2), (0,1,2), (1,0,2), (1,1,2) ]
}

# ==============================================================================
# 3. PROJECTION  (Vector3 imported from engine -- no local duplicate)
# ==============================================================================
def iso_project(x, y, z):
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
        self.shot = None   # active arc-shot descriptor, or None

    def launch_arc(self, tx, ty, tz, peak, dur, make, team, points, shooter):
        self.is_held = False
        self.handler = None
        self.shot = {"sx": self.pos.x, "sy": self.pos.y, "sz": self.pos.z,
                     "tx": tx, "ty": ty, "tz": tz, "peak": peak, "t": 0, "dur": dur,
                     "make": make, "team": team, "points": points, "shooter": shooter}

    def update(self):
        if self.shot:
            s = self.shot
            s["t"] += 1
            f = min(1.0, s["t"] / s["dur"])
            self.pos.x = s["sx"] + (s["tx"] - s["sx"]) * f
            self.pos.y = s["sy"] + (s["ty"] - s["sy"]) * f
            self.pos.z = s["sz"] + (s["tz"] - s["sz"]) * f + math.sin(math.pi * f) * s["peak"]
            return

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
    _font = None

    def __init__(self, x, y, team, stats, name="", role="SF"):
        self.pos = Vector3(x, y, 0.0)
        self.vel = Vector3(0, 0, 0)
        self.team = team
        self.name = name
        self.role = role
        self.state = PlayerState.IDLE
        self.state_timer = 0
        self.shoot_cooldown = 0
        self.is_controlled = False
        self.mark = None  # defensive assignment (an opposing Player)
        self.stats = {
            "speed": stats["speed"],
            "vertical": stats["vertical"],
            "steal": stats["steal"],
            "shooting": stats["shooting"],
        }
        self.jersey_color = (46, 204, 113) if team == "home" else (231, 76, 60)
        self.skin_color = (253, 227, 167)

    def move(self, dx, dy):
        if self.state in (PlayerState.STUNNED, PlayerState.DUNK_ANIMATION, PlayerState.SHOOTING):
            return
        speed_modifier = 0.6 + (self.stats["speed"] / 20.0)
        self.vel.x = dx * speed_modifier
        self.vel.y = dy * speed_modifier
        if dx != 0 or dy != 0:
            self.state = PlayerState.DRIBBLING if self.pos.z == 0 else self.state
        elif self.state == PlayerState.DRIBBLING:
            self.state = PlayerState.IDLE

    def update(self):
        if self.shoot_cooldown > 0:
            self.shoot_cooldown -= 1
        if self.state_timer > 0:
            self.state_timer -= 1
            if self.state_timer == 0:
                self.state = PlayerState.IDLE

        if self.state == PlayerState.DUNK_ANIMATION:
            tx = target_hoop(self.team)[0]
            self.pos.x += (tx - self.pos.x) * 0.15
            self.pos.z = math.sin((self.state_timer / 30.0) * math.pi) * (4.0 + (self.stats["vertical"] / 2.0))
        else:
            self.pos.x += self.vel.x
            self.pos.y += self.vel.y
            # friction so AI steering doesn't drift forever
            self.vel.x *= 0.6
            self.vel.y *= 0.6

        self.pos.x = max(0, min(self.pos.x, COURT_LENGTH))
        self.pos.y = max(0, min(self.pos.y, COURT_WIDTH))

    def draw(self, surface):
        sh_x, sh_y = iso_project(self.pos.x, self.pos.y, 0)
        pygame.draw.ellipse(surface, (15, 15, 20), (sh_x - 14, sh_y - 7, 28, 14))
        if self.is_controlled:
            pygame.draw.ellipse(surface, COLOR_PICK, (sh_x - 16, sh_y - 8, 32, 16), 2)

        for segment, voxels in VOXEL_PLAYER_MESH.items():
            for vx, vy, vz in voxels:
                wx = self.pos.x + (vx * 0.5)
                wy = self.pos.y + (vy * 0.5)
                wz = self.pos.z + (vz * 0.8)
                sx, sy = iso_project(wx, wy, wz)
                if segment == "head": col = self.skin_color
                elif segment == "torso": col = self.jersey_color
                else: col = (44, 62, 80)
                sz = int(0.5 * GRID_SCALE * 2)
                pygame.draw.rect(surface, col, (sx - sz//2, sy - sz//2, sz, sz))

        if self.name:
            if Player._font is None:
                Player._font = pygame.font.SysFont("Courier New", 11, bold=True)
            hx, hy = iso_project(self.pos.x, self.pos.y, self.pos.z + 7)
            tag = Player._font.render(self.name, True, (240, 240, 245))
            surface.blit(tag, (hx - tag.get_width() // 2, hy - 16))

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

        cx, cy = iso_project(COURT_LENGTH // 2, COURT_WIDTH // 2, 0)
        pygame.draw.ellipse(surface, COLOR_LINES, (cx - 50, cy - 25, 100, 50), 3)

        self._hoop(surface, AWAY_HOOP[0], AWAY_HOOP[1], is_left=True)
        self._hoop(surface, HOME_HOOP[0], HOME_HOOP[1], is_left=False)

    def _hoop(self, surface, x, y, is_left):
        pygame.draw.line(surface, (149, 165, 166), iso_project(x, y, 0), iso_project(x, y, 16), 5)
        off = 2.5 if is_left else -2.5
        quad = [iso_project(x + off, y - 5, 14), iso_project(x + off, y + 5, 14),
                iso_project(x + off, y + 5, 20), iso_project(x + off, y - 5, 20)]
        pygame.draw.polygon(surface, (236, 240, 241), quad)
        pygame.draw.polygon(surface, (231, 76, 60), quad, 2)
        rx, ry = iso_project(x + (4.5 if is_left else -4.5), y, RIM_HEIGHT)
        pygame.draw.ellipse(surface, (211, 84, 0), (rx - 10, ry - 5, 20, 10), 3)

# ==============================================================================
# 6. MAIN ENGINE CORE PIPELINE
# ==============================================================================
class GameEngine:
    def __init__(self, home_key="BOS", away_key="DEN", start_in_menu=True):
        pygame.init()
        self.screen = pygame.display.set_mode((SCREEN_WIDTH, SCREEN_HEIGHT))
        pygame.display.set_caption(GAME_TITLE)
        self.clock = pygame.time.Clock()
        self.is_running = True

        self.steam = SteamManager()
        self.court = BasketballCourt()
        self.font_hud = pygame.font.SysFont("Courier New", 16, bold=True)
        self.font_big = pygame.font.SysFont("Courier New", 44, bold=True)
        self.font_mid = pygame.font.SysFont("Courier New", 24, bold=True)

        self.team_keys = list(all_teams().keys())
        self.home_key = home_key if home_key in self.team_keys else self.team_keys[0]
        self.away_key = away_key if away_key in self.team_keys else self.team_keys[1]
        self.menu_home = self.team_keys.index(self.home_key)
        self.menu_away = self.team_keys.index(self.away_key)

        self.scene = "menu" if start_in_menu else "play"
        if self.scene == "play":
            self._setup_match()

    # -- match setup -----------------------------------------------------------
    def _setup_match(self):
        home_name, home5, away_name, away5 = get_arcade_matchup(self.home_key, self.away_key)
        self.home_name, self.away_name = home_name, away_name

        self.home_players = [
            Player(32, 6 + i * 9, "home", p["stats"], name=p["name"], role=p["role"])
            for i, p in enumerate(home5)]
        self.away_players = [
            Player(62, 6 + i * 9, "away", p["stats"], name=p["name"], role=p["role"])
            for i, p in enumerate(away5)]
        self.entities = self.home_players + self.away_players

        # Man-to-man assignments by lineup slot (lineups are role-ordered).
        for i, hp in enumerate(self.home_players):
            hp.mark = self.away_players[min(i, len(self.away_players) - 1)]
        for i, ap in enumerate(self.away_players):
            ap.mark = self.home_players[min(i, len(self.home_players) - 1)]

        self.ball = Ball(COURT_LENGTH / 2, COURT_WIDTH / 2, 12.0)
        self.controlled = self.home_players[0]

        self.score_home = 0
        self.score_away = 0
        self.quarter = 1
        self.game_clock = QUARTER_SECONDS
        self.game_over = False
        self.max_home_deficit = 0
        self.banner = ""
        self.banner_timer = 0
        self.scene = "play"
        self.steam.set_rich_presence("Tip-Off", f"{self.home_name} vs {self.away_name}")

    # -- helpers ---------------------------------------------------------------
    def _teammates(self, team):
        return self.home_players if team == "home" else self.away_players

    def _opponents(self, team):
        return self.away_players if team == "home" else self.home_players

    def _nearest(self, players, x, y):
        return min(players, key=lambda p: distance(p.pos.x, p.pos.y, x, y))

    def _nearest_opp_dist(self, player):
        opp = self._nearest(self._opponents(player.team), player.pos.x, player.pos.y)
        return distance(player.pos.x, player.pos.y, opp.pos.x, opp.pos.y)

    def _update_control(self):
        if self.ball.handler in self.home_players:
            self.controlled = self.ball.handler
        else:
            self.controlled = self._nearest(self.home_players, self.ball.pos.x, self.ball.pos.y)
        for e in self.entities:
            e.is_controlled = (e is self.controlled)

    # -- input -----------------------------------------------------------------
    def process_input(self):
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                self.is_running = False
            elif event.type == pygame.KEYDOWN:
                if event.key == pygame.K_ESCAPE:
                    self.is_running = False
                elif self.scene == "menu":
                    self._menu_key(event.key)
                elif self.scene == "play" and not self.game_over:
                    if event.key == pygame.K_SPACE:
                        self._execute_context_action()
                    elif event.key == pygame.K_e:
                        self._execute_steal_attempt()
                elif self.game_over and event.key == pygame.K_r:
                    self.scene = "menu"

        if self.scene == "play" and not self.game_over:
            keys = pygame.key.get_pressed()
            dx = (keys[pygame.K_d] or keys[pygame.K_RIGHT]) - (keys[pygame.K_a] or keys[pygame.K_LEFT])
            dy = (keys[pygame.K_s] or keys[pygame.K_DOWN]) - (keys[pygame.K_w] or keys[pygame.K_UP])
            self.controlled.move(dx, dy)

    def _menu_key(self, key):
        n = len(self.team_keys)
        if key in (pygame.K_LEFT, pygame.K_a):
            self.menu_home = (self.menu_home - 1) % n
        elif key in (pygame.K_RIGHT, pygame.K_d):
            self.menu_home = (self.menu_home + 1) % n
        elif key in (pygame.K_UP, pygame.K_w):
            self.menu_away = (self.menu_away - 1) % n
        elif key in (pygame.K_DOWN, pygame.K_s):
            self.menu_away = (self.menu_away + 1) % n
        elif key in (pygame.K_RETURN, pygame.K_SPACE):
            self.home_key = self.team_keys[self.menu_home]
            self.away_key = self.team_keys[self.menu_away]
            self._setup_match()

    # -- actions (user) --------------------------------------------------------
    def _execute_context_action(self):
        c = self.controlled
        if self.ball.is_held and self.ball.handler is c:
            hx, hy = target_hoop("home")
            dist = distance(c.pos.x, c.pos.y, hx, hy)
            if dist < DUNK_RANGE and c.stats["vertical"] >= 7 and self._nearest_opp_dist(c) > 3.0:
                self._start_dunk(c)
            else:
                self._launch_shot(c, contested=self._nearest_opp_dist(c) < 4.5)
        else:
            if distance(c.pos.x, c.pos.y, self.ball.pos.x, self.ball.pos.y) < 6.0 and self.ball.pos.z < 5.0 and not self.ball.shot:
                self._give_ball(c)

    def _execute_steal_attempt(self):
        c = self.controlled
        if not self.ball.is_held or self.ball.handler is c or self.ball.handler.team == "home":
            return
        target = self.ball.handler
        if distance(c.pos.x, c.pos.y, target.pos.x, target.pos.y) < 7.0:
            if random.random() < 0.2 + (c.stats["steal"] * 0.05):
                target.state = PlayerState.STUNNED
                target.state_timer = 45
                self._give_ball(c)
                self.steam.unlock_achievement("CLEAN_STEAL")

    # -- shots / possession ----------------------------------------------------
    def _give_ball(self, player):
        self.ball.is_held = True
        self.ball.handler = player
        self.ball.shot = None
        player.state = PlayerState.DRIBBLING

    def _launch_shot(self, shooter, contested):
        hx, hy = target_hoop(shooter.team)
        dist = distance(shooter.pos.x, shooter.pos.y, hx, hy)
        points = point_value(shooter.pos.x, shooter.pos.y, shooter.team)
        made = random.random() < make_probability(shooter.stats["shooting"], dist, contested)
        dur = int(20 + dist * 0.4)
        peak = 6 + dist * 0.25
        if made:
            tx, ty = hx, hy
        else:
            tx, ty = hx + random.uniform(-3.5, 3.5), hy + random.uniform(-3.5, 3.5)
        shooter.state = PlayerState.SHOOTING
        shooter.state_timer = 15
        shooter.shoot_cooldown = 45
        self.ball.launch_arc(tx, ty, RIM_HEIGHT, peak, dur, made, shooter.team, points, shooter)
        if shooter.is_controlled:
            self.steam.set_rich_presence("In Game", "Taking a shot")

    def _start_dunk(self, shooter):
        shooter.state = PlayerState.DUNK_ANIMATION
        shooter.state_timer = 30
        shooter.shoot_cooldown = 45
        if shooter.is_controlled:
            self.steam.unlock_achievement("FIRST_DUNK")
            self.steam.set_rich_presence("In Game", "Throwing down a dunk!")

    def _resolve_shot(self):
        s = self.ball.shot
        self.ball.shot = None
        if s["make"]:
            self._award(s["team"], s["points"], scorer=s["shooter"])
        else:
            # Rebound: ball drops off the rim and becomes loose.
            self.ball.pos = Vector3(s["tx"], s["ty"], RIM_HEIGHT - 1.0)
            self.ball.vel = Vector3(random.uniform(-0.6, 0.6), random.uniform(-0.6, 0.6), 0.4)
            self.ball.is_held = False
            self.ball.handler = None

    def _award(self, team, points, scorer=None, dunk=False):
        if team == "home":
            self.score_home += points
        else:
            self.score_away += points
        name = scorer.name if scorer else team
        self.banner = f"{name}  +{points}" + ("  DUNK!" if dunk else "")
        self.banner_timer = 90

        if self.score_home + self.score_away == points:
            self.steam.unlock_achievement("FIRST_BASKET")
        if self.game_clock <= 3.0:
            self.steam.unlock_achievement("BUZZER_BEATER")
        self.steam.set_rich_presence("Score", f"{self.home_name} {self.score_home}-{self.score_away} {self.away_name}")
        self._reset_ball_center()

    def _reset_ball_center(self):
        self.ball.is_held = False
        self.ball.handler = None
        self.ball.shot = None
        self.ball.pos = Vector3(COURT_LENGTH / 2, COURT_WIDTH / 2, 9.0)
        self.ball.vel = Vector3(0, 0, 0)

    # -- AI --------------------------------------------------------------------
    def _process_ai(self):
        loose = (not self.ball.is_held) or (self.ball.shot is not None)
        for e in self.entities:
            if e is self.controlled or e.state in (PlayerState.STUNNED, PlayerState.DUNK_ANIMATION, PlayerState.SHOOTING):
                continue
            mates = self._teammates(e.team)

            if loose:
                if e is self._nearest(mates, self.ball.pos.x, self.ball.pos.y):
                    self._steer(e, self.ball.pos.x, self.ball.pos.y)
                else:
                    self._steer(e, *offensive_spot(e.team, e.role))
                continue

            handler = self.ball.handler
            if handler.team == e.team:
                if handler is e:
                    self._ai_with_ball(e)
                else:
                    self._steer(e, *offensive_spot(e.team, e.role))
            else:
                self._ai_defend(e, handler)

    def _ai_with_ball(self, e):
        hx, hy = target_hoop(e.team)
        dist = distance(e.pos.x, e.pos.y, hx, hy)
        contested = self._nearest_opp_dist(e) < 4.5
        if dist < DUNK_RANGE and e.stats["vertical"] >= 7 and not contested and random.random() < AI_DUNK_CHANCE:
            self._start_dunk(e)
            return
        if dist < SHOOT_RANGE and e.shoot_cooldown == 0:
            shoot_chance = AI_SHOOT_CHANCE_OPEN if not contested else AI_SHOOT_CHANCE_CONTESTED
            if random.random() < shoot_chance:
                self._launch_shot(e, contested)
                return
            if contested and random.random() < 0.04:
                self._ai_pass(e)
                return
        self._steer(e, hx, hy)  # drive

    def _ai_pass(self, e):
        hx, hy = target_hoop(e.team)
        mates = [m for m in self._teammates(e.team) if m is not e and m.state != PlayerState.STUNNED]
        if mates:
            self._give_ball(self._nearest(mates, hx, hy))

    def _ai_defend(self, e, handler):
        bx, by = target_hoop(handler.team)   # basket the offense attacks
        mark = e.mark or handler
        tx = mark.pos.x * 0.6 + bx * 0.4
        ty = mark.pos.y * 0.6 + by * 0.4
        self._steer(e, tx, ty)
        if mark is handler and distance(e.pos.x, e.pos.y, handler.pos.x, handler.pos.y) < 3.5:
            if random.random() < 0.008 + e.stats["steal"] * 0.003:
                handler.state = PlayerState.STUNNED
                handler.state_timer = 30
                self._give_ball(e)

    def _steer(self, e, tx, ty):
        dx = 1 if tx > e.pos.x + 0.6 else (-1 if tx < e.pos.x - 0.6 else 0)
        dy = 1 if ty > e.pos.y + 0.6 else (-1 if ty < e.pos.y - 0.6 else 0)
        e.move(dx, dy)

    # -- update ----------------------------------------------------------------
    def update(self, dt):
        if self.scene != "play" or self.game_over:
            return

        self.game_clock -= dt
        if self.game_clock <= 0:
            self._end_period()
            return
        if self.banner_timer > 0:
            self.banner_timer -= 1
            if self.banner_timer == 0:
                self.banner = ""

        self._update_control()
        self._process_ai()
        for e in self.entities:
            e.update()
        self.ball.update()

        # Resolve a completed arc shot.
        if self.ball.shot and self.ball.shot["t"] >= self.ball.shot["dur"]:
            self._resolve_shot()

        # Dunk finishes score for the dunker's team.
        for e in self.entities:
            if e.state == PlayerState.DUNK_ANIMATION and e.state_timer == 1:
                self._award(e.team, 2, scorer=e, dunk=True)

        # Out of bounds -> turnover.
        if not self.ball.is_held and not self.ball.shot and not in_bounds(self.ball.pos.x, self.ball.pos.y):
            self._reset_ball_center()

        # Loose-ball pickup.
        if not self.ball.is_held and not self.ball.shot:
            for e in self.entities:
                if e.state == PlayerState.STUNNED:
                    continue
                if distance(e.pos.x, e.pos.y, self.ball.pos.x, self.ball.pos.y) < 3.5 and self.ball.pos.z < 4.0:
                    self._give_ball(e)
                    break

        self.max_home_deficit = max(self.max_home_deficit, self.score_away - self.score_home)

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
        if self.scene == "menu":
            self._render_menu()
        else:
            self._render_game()
        pygame.display.flip()

    def _render_game(self):
        self.screen.fill(COLOR_BG)
        self.court.draw(self.screen)

        queue = [(e.pos.y, e) for e in self.entities]
        if not self.ball.is_held:
            queue.append((self.ball.pos.y, self.ball))
        queue.sort(key=lambda el: el[0])
        for _, obj in queue:
            obj.draw(self.screen)
        if self.ball.is_held:
            self.ball.draw(self.screen)

        self._render_hud()
        if self.game_over:
            self._render_final()

    def _render_hud(self):
        f = self.font_hud
        period = f"Q{self.quarter}" if self.quarter <= NUM_QUARTERS else f"OT{self.quarter - NUM_QUARTERS}"
        score = f.render(f"{self.home_name} {self.score_home}  -  {self.score_away} {self.away_name}    [{period}  {format_clock(self.game_clock)}]", True, (241, 196, 15))
        ctrls = f.render("WASD/Arrows: Move (controls ball-carrier / nearest) | Space: Shoot/Dunk/Catch | E: Steal", True, (220, 220, 230))
        c = self.controlled
        stats = f.render(f"{c.name} ({c.role}) -> SPD {c.stats['speed']} VERT {c.stats['vertical']} STL {c.stats['steal']} SHT {c.stats['shooting']}", True, (52, 152, 219))
        self.screen.blit(score, (25, 18))
        self.screen.blit(ctrls, (25, 42))
        self.screen.blit(stats, (25, SCREEN_HEIGHT - 35))
        if self.banner:
            b = self.font_big.render(self.banner, True, (255, 255, 255))
            self.screen.blit(b, (SCREEN_WIDTH // 2 - b.get_width() // 2, 86))

    def _render_final(self):
        winner = self.home_name if self.score_home > self.score_away else self.away_name
        l1 = self.font_big.render("FINAL", True, (255, 255, 255))
        l2 = self.font_big.render(f"{self.home_name} {self.score_home} - {self.score_away} {self.away_name}", True, (241, 196, 15))
        l3 = self.font_hud.render(f"{winner} win!   R: team select   ESC: quit", True, (220, 220, 230))
        cx = SCREEN_WIDTH // 2
        self.screen.blit(l1, (cx - l1.get_width() // 2, 280))
        self.screen.blit(l2, (cx - l2.get_width() // 2, 336))
        self.screen.blit(l3, (cx - l3.get_width() // 2, 404))

    def _render_menu(self):
        self.screen.fill(COLOR_BG)
        cx = SCREEN_WIDTH // 2
        title = self.font_big.render("VOXEL HOOPS", True, (242, 108, 31))
        self.screen.blit(title, (cx - title.get_width() // 2, 90))

        home_key = self.team_keys[self.menu_home]
        away_key = self.team_keys[self.menu_away]
        names = all_teams()

        home = self.font_mid.render(f"HOME  (<-/->):   {names[home_key]}", True, (46, 204, 113))
        away = self.font_mid.render(f"AWAY  (up/down): {names[away_key]}", True, (231, 76, 60))
        self.screen.blit(home, (cx - home.get_width() // 2, 240))
        self.screen.blit(away, (cx - away.get_width() // 2, 300))

        hint = self.font_hud.render("Pick teams, then ENTER to tip off.   You control HOME.   ESC: quit", True, (220, 220, 230))
        self.screen.blit(hint, (cx - hint.get_width() // 2, 400))

    def run(self):
        while self.is_running:
            dt = self.clock.tick(FPS) / 1000.0
            self.process_input()
            self.update(dt)
            self.render()
        pygame.quit()
        sys.exit()


def main():
    parser = argparse.ArgumentParser(description="Voxel Hoops -- real-time 5-on-5 arcade basketball")
    parser.add_argument("--home", default="BOS", help="home team key")
    parser.add_argument("--away", default="DEN", help="away team key")
    parser.add_argument("--no-menu", action="store_true", help="skip the team-select menu")
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

    GameEngine(home_key, away_key, start_in_menu=not args.no_menu).run()


if __name__ == "__main__":
    main()
