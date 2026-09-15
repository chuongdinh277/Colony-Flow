using UnityEngine;

namespace ColonyFlow
{
    public sealed class PixelPickupFx : MonoBehaviour
    {
        private static PixelPickupFx instance;
        public static PixelPickupFx Ins
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("PixelPickupFxManager");
                    instance = go.AddComponent<PixelPickupFx>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private ParticleSystem particleSys;

        private void Awake()
        {
            if (instance == null) instance = this;
            SetupParticleSystem();
        }

        private void SetupParticleSystem()
        {
            if (particleSys != null) return;
            particleSys = gameObject.AddComponent<ParticleSystem>();

            var main = particleSys.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 0.35f;
            main.startSpeed = 1.6f;
            main.startSize = 0.09f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;

            var emission = particleSys.emission;
            emission.enabled = false;

            var shape = particleSys.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var colorOverLifetime = particleSys.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = particleSys.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(1f, 0.2f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var pRenderer = GetComponent<ParticleSystemRenderer>();
            pRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            pRenderer.material = RuntimeSprite.MaterialFor(RuntimeSprite.RoundedSquare);
        }

        public void PlayAt(Vector3 position, Color color)
        {
            if (particleSys == null) SetupParticleSystem();

            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                startColor = color,
                applyShapeToPosition = true
            };

            particleSys.Emit(emitParams, 9);
        }
    }
}
