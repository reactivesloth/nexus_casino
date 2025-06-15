using UnityEngine;

public class UVDiscoFloor : MonoBehaviour
{ 
    [SerializeField] private int materialID = 0;
    [SerializeField] private float interval = 1;
    
    private float offset; 
    private Renderer renderer;
    private float saveTime;
    
    private void Start()
    {
        offset = Random.Range(0f, 100f);
        renderer = GetComponent<Renderer>();
    }
    
    public void Update()
    {
        saveTime -= Time.deltaTime;

        if (saveTime <= 0)
        {
            renderer.materials[materialID].mainTextureOffset = new Vector2(1/(Mathf.Pow(2, Random.Range(1, 6))), 1/(Mathf.Pow(2, Random.Range(1, 6))));

            saveTime = interval;
        }
    }
}