using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class AOAnimatedSprite : MonoBehaviour
{
    [SerializeField] Sprite[] frames;
    [SerializeField] float fps = 8f;
    [SerializeField] bool playing = true;

    SpriteRenderer target;
    float elapsed;
    int displayedIndex = -1;
    static readonly int BoundsProperty = Shader.PropertyToID("_AOBounds");
    MaterialPropertyBlock boundsProperties;

    public void Configure(Sprite[] newFrames, float newFps)
    {
        frames = newFrames ?? new Sprite[0];
        fps = Mathf.Clamp(newFps <= 0f ? 8f : newFps, 1f, 60f);
        target = GetComponent<SpriteRenderer>();
        elapsed = 0f;
        displayedIndex = -1;
        Refresh();
    }

    void Awake()
    {
        target = GetComponent<SpriteRenderer>();
        Refresh();
    }

    void Update()
    {
        if (!playing || frames == null || frames.Length <= 1) return;
        elapsed += AOMainMenuV140.ModalOpen
            ? Time.unscaledDeltaTime
            : Time.deltaTime;
        Refresh();
    }

    void Refresh()
    {
        if (target == null) target = GetComponent<SpriteRenderer>();
        if (frames == null || frames.Length == 0) return;
        int index = frames.Length <= 1
            ? 0
            : Mathf.FloorToInt(elapsed * fps) % frames.Length;
        index = Mathf.Clamp(index, 0, frames.Length - 1);
        if (index == displayedIndex) return;
        displayedIndex = index;
        target.sprite = frames[index];
        if (target.sharedMaterial == null ||
            (target.sharedMaterial.shader.name != "AO/MapVertexLit" &&
             target.sharedMaterial.shader.name != "AO/ParticleVertexAdditive")) return;
        if (boundsProperties == null)
            boundsProperties = new MaterialPropertyBlock();
        target.GetPropertyBlock(boundsProperties);
        Bounds bounds = frames[index].bounds;
        boundsProperties.SetVector(BoundsProperty, new Vector4(
            bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
        target.SetPropertyBlock(boundsProperties);
    }
}
