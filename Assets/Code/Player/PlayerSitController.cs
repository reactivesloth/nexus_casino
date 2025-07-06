using UnityEngine;

namespace Code.Player
{
    public class PlayerSitController : MonoBehaviour
    {
        public void SitAt(Vector3 interactionPointPosition, Quaternion interactionPointRotation)
        {
            Debug.Log("PlayerSitController::SitAt(interactionPointPosition, interactionPointRotation)");
        }
    }
}