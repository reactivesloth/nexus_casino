using UnityEngine;

public class UVScroller : MonoBehaviour
{
    [SerializeField] private Vector2 scrollSpeed;
    [SerializeField] private int materialID = 0;

    private float offset; 
    private Renderer renderer;

    private void Start()
    {
        offset = Random.Range(0f, 100f);
        renderer = GetComponent<Renderer>();
    }
    
    public void Update()
    {
        if (scrollSpeed != Vector2.zero)
        {
            renderer.materials[materialID].mainTextureOffset = (Time.time + offset) * scrollSpeed;
        }
    }
}
