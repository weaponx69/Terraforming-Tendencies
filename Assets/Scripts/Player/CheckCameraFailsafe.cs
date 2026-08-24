using UnityEngine;
using Unity.Cinemachine;
namespace GameDevTV.RTS.Player {
    public class CheckCameraFailsafe : MonoBehaviour {
        void Start() {
            var cam = FindAnyObjectByType<CinemachineCamera>();
            if (cam != null) {
                if (cam.Follow == null) {
                    Debug.LogError("[CheckCameraFailsafe] CRITICAL: CinemachineCamera has NO Follow target! Panning and Zooming will do absolutely nothing!");
                    var target = GameObject.Find("Camera Target");
                    if (target != null) {
                        cam.Follow = target.transform;
                        Debug.Log("[CheckCameraFailsafe] Fixed! Assigned Camera Target to Follow.");
                    }
                } else {
                    Debug.Log("[CheckCameraFailsafe] CinemachineCamera Follow is assigned to: " + cam.Follow.name);
                }
            }
        }
    }
}
