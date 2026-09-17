using UnityEngine;
using UnityEngine.Rendering;

namespace ColonyFlow
{
    public sealed class GameplayLightingRig : MonoBehaviour
    {
        [SerializeField] private Light keyLight;
        [SerializeField] private Light fillLight;
        [SerializeField] private Color keyColor = new(1f, .92f, .82f, 1f);
        [SerializeField] private Color fillColor = new(.55f, .72f, 1f, 1f);

        public void Initialize()
        {
            if (keyLight == null) keyLight = CreateLight("Key Light", keyColor, 1.25f, new Vector3(50f, 135f, 0f));
            else {
                keyLight.color = keyColor;
                keyLight.intensity = 1.25f;
                keyLight.transform.localRotation = Quaternion.Euler(50f, 135f, 0f);
            }
            
            if (fillLight == null) fillLight = CreateLight("Fill Light", fillColor, 0.48f, new Vector3(-24f, -45f, 0f));
            else {
                fillLight.color = fillColor;
                fillLight.intensity = 0.48f;
                fillLight.transform.localRotation = Quaternion.Euler(-24f, -45f, 0f);
            }
            
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.46f, .46f, .5f, 1f);
        }

        private Light CreateLight(string lightName, Color color, float intensity, Vector3 rotation)
        {
            Transform child = new GameObject(lightName).transform;
            child.SetParent(transform, false);
            child.localRotation = Quaternion.Euler(rotation);
            Light light = child.gameObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = (lightName == "Key Light") ? LightShadows.Soft : LightShadows.None;
            if (lightName == "Key Light")
            {
                light.shadowStrength = 0.65f;
                light.shadowNormalBias = 0.1f;
                light.shadowBias = 0.05f;
            }
            return light;
        }
    }
}
