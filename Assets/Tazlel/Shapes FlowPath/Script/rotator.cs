using UnityEngine;

namespace Tazlel.ShapesPathFlow.Example
{
    public class rotator : MonoBehaviour
    {
    public float speed = 100f;
    
    // Update is called once per frame
    void Update()
    {
    transform.Rotate(0f, 0f, speed * Time.deltaTime);
    }
    }
}
