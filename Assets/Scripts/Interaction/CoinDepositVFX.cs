using UnityEngine;

namespace Octo.Interaction
{
    /// <summary>
    /// Spawns a burst of particles + light flash when a coin is deposited.
    /// Created procedurally — no prefab needed.
    /// Call Play(position) to trigger.
    /// </summary>
    public class CoinDepositVFX : MonoBehaviour
    {
        private ParticleSystem burstPS;
        private Light flashLight;
        private float flashTimer;

        private void Awake()
        {
            burstPS = CreateBurstParticles();
            flashLight = CreateFlashLight();
        }

        private void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                flashLight.intensity = Mathf.Lerp(0f, 3f, flashTimer / 0.3f);
                if (flashTimer <= 0f)
                    flashLight.enabled = false;
            }
        }

        public void Play(Vector3 position)
        {
            transform.position = position;
            burstPS.transform.position = position;
            burstPS.Play();

            flashLight.transform.position = position + Vector3.up * 0.3f;
            flashLight.enabled = true;
            flashTimer = 0.3f;
        }

        private ParticleSystem CreateBurstParticles()
        {
            var go = new GameObject("CoinBurst");
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.6f;
            main.startSpeed = 3f;
            main.startSize = 0.08f;
            main.startColor = new Color(1f, 0.85f, 0.2f); // gold
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.5f;
            main.playOnAwake = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0f),
                    new GradientColorKey(new Color(1f, 0.6f, 0.1f), 1f)
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            renderer.material.color = new Color(1f, 0.85f, 0.2f);

            return ps;
        }

        private Light CreateFlashLight()
        {
            var go = new GameObject("CoinFlash");
            go.transform.SetParent(transform, false);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.9f, 0.4f);
            light.range = 3f;
            light.intensity = 0f;
            light.enabled = false;

            return light;
        }
    }
}
