using NUnit.Framework;
using Shift9.Presentation;
using Shift9.Sim.Core;
using UnityEngine;

namespace Shift9.Presentation.Tests
{
    public sealed class BroadcastCameraTests
    {
        [Test]
        public void Camera_SitsOffNearSideline_AimsAcrossAndDown()
        {
            var cfg = BroadcastCameraConfig.Default;
            CameraPose pose = BroadcastCamera.ComputePose(cfg, Vector3.zero);

            // Fixed rig: -x sideline, elevated, beyond the line.
            Assert.AreEqual(-(SimConstants.CourtHalfWidth + cfg.SidelineSetback), pose.Position.x, 0.001f);
            Assert.AreEqual(cfg.Height, pose.Position.y, 0.001f);

            Vector3 fwd = pose.Rotation * Vector3.forward;
            Assert.Greater(fwd.x, 0f);  // looks across the court toward +x
            Assert.Less(fwd.y, 0f);     // looks downward
        }

        [Test]
        public void Camera_YawsToFollowTargetDownCourt()
        {
            var cfg = BroadcastCameraConfig.Default;
            float toPositive = (BroadcastCamera.ComputePose(cfg, new Vector3(0, 0, 30f)).Rotation * Vector3.forward).z;
            float toNegative = (BroadcastCamera.ComputePose(cfg, new Vector3(0, 0, -30f)).Rotation * Vector3.forward).z;

            Assert.Greater(toPositive, 0f); // ball toward +z -> camera yaws that way
            Assert.Less(toNegative, 0f);    // ball toward -z -> camera yaws the other way
        }

        [Test]
        public void Camera_FarSideFlipsToPositiveX()
        {
            var cfg = BroadcastCameraConfig.Default;
            cfg.NearSideNegativeX = false;
            CameraPose pose = BroadcastCamera.ComputePose(cfg, Vector3.zero);
            Assert.Greater(pose.Position.x, 0f);
            Assert.Less((pose.Rotation * Vector3.forward).x, 0f); // now looks back toward -x
        }

        [Test]
        public void Camera_DefaultTiltIsInBroadcastBand()
        {
            // Reference (NBA 2K broadcast / real arena cam): 10-25 deg downward tilt.
            float tilt = BroadcastCamera.TiltDegrees(BroadcastCameraConfig.Default);
            Assert.GreaterOrEqual(tilt, 10f);
            Assert.LessOrEqual(tilt, 25f);
        }

        [Test]
        public void Camera_PassesThroughFieldOfView()
        {
            var cfg = BroadcastCameraConfig.Default;
            cfg.FieldOfView = 27.5f;
            Assert.AreEqual(27.5f, BroadcastCamera.ComputePose(cfg, Vector3.zero).FieldOfView, 0.001f);
        }
    }
}
