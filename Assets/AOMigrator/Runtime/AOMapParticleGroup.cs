using System;
using UnityEngine;

[Serializable]
public class AOMapParticlePlacement
{
    public int x;
    public int y;
    public int particle;
}

[Serializable]
public class AOMapParticleSprite
{
    public int grh;
    public AOWorldManagerV07.FrameSpec[] frames;
    public float fps;
}

[Serializable]
public class AOMapParticleDefinition
{
    public int id;
    public string name;
    public int count;
    public int[] origin;
    public int[] velocity;
    public int[] life;
    public int friction;
    public bool spin;
    public float[] spinSpeed;
    public bool alphaBlend;
    public bool gravity;
    public int gravityStrength;
    public int bounceStrength;
    public bool moveX;
    public bool moveY;
    public int[] moveBounds;
    public int[] tint;
    public int[] cornerColors;
    public int angle;
    public int lifeCounter;
    public float speed;
    public bool resize;
    public int resizeX;
    public int resizeY;
    public AOMapParticleSprite[] sprites;
}

[Serializable]
public class AOMapParticleLibrary
{
    public string version;
    public AOMapParticleDefinition[] definitions;
    public AOMapParticleSprite[] fogSprites;
}

[DisallowMultipleComponent]
public class AOMapParticleGroup : MonoBehaviour
{
    struct ParticleState
    {
        public float x;
        public float y;
        public float vx;
        public float vy;
        public int life;
    }

    AOMapParticleDefinition definition;
    Sprite[][] spriteOptions;
    Camera gameCamera;
    SpriteRenderer[] renderers;
    ParticleState[] particles;
    float elapsed;
    static Material additiveMaterial;
    static Material normalMaterial;
    MaterialPropertyBlock colorProperties;
    int groupLifeRemaining;
    bool destroying;
    static readonly int[] ColorIds = {
        Shader.PropertyToID("_AOColor0"), Shader.PropertyToID("_AOColor1"),
        Shader.PropertyToID("_AOColor2"), Shader.PropertyToID("_AOColor3")
    };
    static readonly int BoundsId = Shader.PropertyToID("_AOBounds");

    public void Configure(AOMapParticleDefinition newDefinition,
                          Sprite[][] newSpriteOptions,
                          Camera newCamera,
                          int tileX, int tileY)
    {
        definition = newDefinition;
        spriteOptions = newSpriteOptions;
        gameCamera = newCamera;
        transform.position = new Vector3(tileX - 0.5f, -tileY, 0f);
        groupLifeRemaining = definition.lifeCounter;
        destroying = false;
    }

    void Update()
    {
        if (definition == null || spriteOptions == null ||
            spriteOptions.Length == 0)
            return;

        if (gameCamera == null)
            gameCamera = Camera.main;
        if (gameCamera == null)
            return;

        Vector3 cameraPosition = gameCamera.transform.position;
        bool inView = Mathf.Abs(cameraPosition.x - transform.position.x) < 16f &&
                      Mathf.Abs(cameraPosition.y - transform.position.y) < 12f;
        if (!inView)
        {
            if (renderers != null)
                foreach (SpriteRenderer renderer in renderers)
                    renderer.enabled = false;
            return;
        }

        if (renderers == null)
            CreateParticles();
        foreach (SpriteRenderer renderer in renderers)
            renderer.enabled = true;

        // VB6: timerTicksPerFrame = elapsedMilliseconds * engineBaseSpeed (0.018).
        elapsed += (AOMainMenuV140.ModalOpen
            ? Time.unscaledDeltaTime
            : Time.deltaTime) * 18f;
        if (elapsed > Mathf.Max(0.001f, definition.speed))
        {
            elapsed = 0f;
            Step();
        }
    }

    void CreateParticles()
    {
        int count = Mathf.Clamp(definition.count, 0, 200);
        renderers = new SpriteRenderer[count];
        particles = new ParticleState[count];
        int red = definition.tint != null && definition.tint.Length > 0
            ? definition.tint[0] : 255;
        int green = definition.tint != null && definition.tint.Length > 1
            ? definition.tint[1] : 255;
        int blue = definition.tint != null && definition.tint.Length > 2
            ? definition.tint[2] : 255;
        Color32 tint = new Color32((byte)Mathf.Clamp(red, 0, 255),
                                   (byte)Mathf.Clamp(green, 0, 255),
                                   (byte)Mathf.Clamp(blue, 0, 255), 255);

        if (normalMaterial == null)
        {
            Shader shader = Resources.Load<Shader>(
                "AOMigrator/WorldV07/AOMapVertexLit");
            if (shader != null)
                normalMaterial = new Material(shader);
        }
        if (definition.alphaBlend && additiveMaterial == null)
        {
            Shader shader = Resources.Load<Shader>(
                "AOMigrator/WorldV07/AOParticleVertexAdditive");
            if (shader != null) additiveMaterial = new Material(shader);
        }
        colorProperties = new MaterialPropertyBlock();

        for (int i = 0; i < count; i++)
        {
            GameObject child = new GameObject("Particula_" + i,
                                               typeof(SpriteRenderer));
            child.transform.SetParent(transform, false);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            renderer.color = Color.white;
            if (definition.alphaBlend && additiveMaterial != null)
                renderer.sharedMaterial = additiveMaterial;
            else if (normalMaterial != null)
                renderer.sharedMaterial = normalMaterial;
            else renderer.color = tint;
            if (renderer.sharedMaterial == normalMaterial ||
                renderer.sharedMaterial == additiveMaterial)
            {
                colorProperties.Clear();
                for (int corner = 0; corner < 4; corner++)
                    colorProperties.SetColor(ColorIds[corner], CornerColor(corner, tint));
                renderer.SetPropertyBlock(colorProperties);
            }
            renderer.sortingOrder = 10000 - Mathf.RoundToInt(transform.position.y);
            renderers[i] = renderer;
            Restart(i);
        }
    }

    void Step()
    {
        float friction = Mathf.Max(1, definition.friction);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleState state = particles[i];
            if (state.life <= 0)
            {
                if (destroying)
                { renderers[i].enabled = false; continue; }
                Restart(i);
                state = particles[i];
            }
            else
            {
                if (definition.gravity)
                {
                    state.vy += definition.gravityStrength;
                    if (state.y > 0f)
                        state.vy = definition.bounceStrength;
                }
                if (definition.moveX && definition.moveBounds != null &&
                    definition.moveBounds.Length >= 2)
                    state.vx = RandomBetween(definition.moveBounds[0],
                                             definition.moveBounds[1]);
                if (definition.moveY && definition.moveBounds != null &&
                    definition.moveBounds.Length >= 4)
                    state.vy = RandomBetween(definition.moveBounds[2],
                                             definition.moveBounds[3]);
                if (definition.spin && definition.spinSpeed != null &&
                    definition.spinSpeed.Length >= 2)
                    renderers[i].transform.Rotate(0f, 0f,
                        UnityEngine.Random.Range(definition.spinSpeed[0],
                                                 definition.spinSpeed[1]) / 5f);
            }
            state.x += (int)(state.vx / friction);
            state.y += (int)(state.vy / friction);
            state.life--;
            particles[i] = state;
            Position(i);
        }
        if (!destroying && groupLifeRemaining > 0 && --groupLifeRemaining == 0)
            destroying = true;
        if (destroying)
        {
            bool anyAlive = false;
            foreach (ParticleState state in particles)
                if (state.life > 0) { anyAlive = true; break; }
            if (!anyAlive) Destroy(gameObject);
        }
    }

    void Restart(int index)
    {
        int option = UnityEngine.Random.Range(0, spriteOptions.Length);
        Sprite[] frames = spriteOptions[option];
        if (frames == null || frames.Length == 0)
            return;

        SpriteRenderer renderer = renderers[index];
        renderer.transform.localRotation = Quaternion.Euler(0f, 0f, definition.angle);
        renderer.sprite = frames[0];
        AOAnimatedSprite animation =
            renderer.GetComponent<AOAnimatedSprite>();
        if (frames.Length > 1)
        {
            if (animation == null)
                animation = renderer.gameObject.AddComponent<AOAnimatedSprite>();
            animation.enabled = true;
            animation.Configure(frames, definition.sprites[option].fps);
        }
        else if (animation != null)
            animation.enabled = false;

        if (colorProperties != null && renderer.sharedMaterial != null &&
            (renderer.sharedMaterial == normalMaterial ||
             renderer.sharedMaterial == additiveMaterial))
        {
            renderer.GetPropertyBlock(colorProperties);
            Bounds bounds = frames[0].bounds;
            colorProperties.SetVector(BoundsId, new Vector4(
                bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
            renderer.SetPropertyBlock(colorProperties);
        }

        ParticleState state = new ParticleState();
        if (definition.origin != null && definition.origin.Length >= 4)
        {
            state.x = RandomBetween(definition.origin[0],
                                    definition.origin[2]) - 16;
            state.y = RandomBetween(definition.origin[1],
                                    definition.origin[3]) - 16;
        }
        if (definition.velocity != null && definition.velocity.Length >= 4)
        {
            state.vx = RandomBetween(definition.velocity[0],
                                     definition.velocity[1]);
            state.vy = RandomBetween(definition.velocity[2],
                                     definition.velocity[3]);
        }
        state.life = definition.life != null && definition.life.Length >= 2
            ? Mathf.Max(1, RandomBetween(definition.life[0],
                                         definition.life[1])) : 1;
        particles[index] = state;
        Position(index);

        if (definition.resize && definition.resizeX > 0 &&
            definition.resizeY > 0)
        {
            Rect rect = renderer.sprite.rect;
            renderer.transform.localScale = new Vector3(
                definition.resizeX / rect.width,
                definition.resizeY / rect.height, 1f);
        }
    }

    void Position(int index)
    {
        ParticleState state = particles[index];
        renderers[index].transform.localPosition =
            new Vector3(state.x / 32f, -state.y / 32f, 0f);
    }

    static int RandomBetween(int a, int b)
    {
        return UnityEngine.Random.Range(Mathf.Min(a, b), Mathf.Max(a, b) + 1);
    }

    Color32 CornerColor(int corner, Color32 fallback)
    {
        int offset = corner * 3;
        if (definition.cornerColors == null ||
            definition.cornerColors.Length < offset + 3) return fallback;
        int[] bgr = definition.cornerColors;
        return new Color32((byte)Mathf.Clamp(bgr[offset + 2], 0, 255),
                           (byte)Mathf.Clamp(bgr[offset + 1], 0, 255),
                           (byte)Mathf.Clamp(bgr[offset], 0, 255), 255);
    }
}
