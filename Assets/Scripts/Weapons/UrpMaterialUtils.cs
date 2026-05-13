using UnityEngine;
using UnityEngine.Rendering;

namespace AcesOverTheLines.Weapons
{
    // Runtime-constructed URP particle/unlit materials default to OPAQUE
    // mode (queue 2000, _Surface=0, _Blend=0, _SrcBlend=One/_DstBlend=Zero,
    // _ZWrite=1) regardless of the intended use. When the ParticleSystemRenderer
    // draws untextured billboards through an opaque-queued material, URP's
    // SRP batcher produces the magenta pink fallback because the queue /
    // keyword combination doesn't match what the shader's transparent
    // variants expect.
    //
    // These helpers apply the standard transparent setup so a particle
    // material renders correctly. Pick Additive for self-luminous effects
    // (muzzle flash, hit sparks) and AlphaBlended for occluders (smoke).
    public static class UrpMaterialUtils
    {
        // Self-luminous, screen-brightening blend (One / One). Best for
        // muzzle flashes, hit sparks, bright explosions.
        public static void ConfigureUrpParticleAdditive(Material mat)
        {
            mat.SetFloat("_Surface", 1f);   // 0=Opaque, 1=Transparent
            mat.SetFloat("_Blend", 2f);     // 0=Alpha, 1=Premul, 2=Additive, 3=Multiply
            mat.SetFloat("_SrcBlend", (float)BlendMode.One);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        // Standard alpha blending (SrcAlpha / OneMinusSrcAlpha). Best for
        // smoke / occluders — the particle's alpha controls how much of
        // the background shows through, so dark colours stay dark instead
        // of brightening the screen the way additive would.
        public static void ConfigureUrpParticleAlphaBlended(Material mat)
        {
            mat.SetFloat("_Surface", 1f);   // Transparent
            mat.SetFloat("_Blend", 0f);     // Alpha
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
