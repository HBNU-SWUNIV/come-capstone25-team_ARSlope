using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    void Update()
    {
        this.transform.rotation = Quaternion.LookRotation(this.transform.position - Camera.main.transform.position);
    }
}
