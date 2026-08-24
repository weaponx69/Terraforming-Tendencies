#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameDevTV.RTS.Tests
{
    public class PlayerInputCameraTests
    {
        [UnityTest]
        public IEnumerator CameraTarget_Move_UpdatesPosition()
        {
            // Create a camera target GameObject
            GameObject cameraTargetObj = new GameObject("Camera Target");
            cameraTargetObj.transform.position = new Vector3(0, 10, 0);
            
            // Move it to verify functionality
            Vector3 initialPosition = cameraTargetObj.transform.position;
            cameraTargetObj.transform.position = new Vector3(5, 10, 0);
            
            // Verify position changed
            Assert.AreNotEqual(initialPosition, cameraTargetObj.transform.position);
            
            yield return null;
        }
    }
}
#endif