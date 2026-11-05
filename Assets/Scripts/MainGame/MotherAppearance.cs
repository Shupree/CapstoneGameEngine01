using System.Collections.Generic;
using UnityEngine;

// 편집 화면과 런타임에 같은 어두운 외형을 적용합니다. 인스턴스별 PropertyBlock으로 원본 머티리얼·텍스처를 보존합니다.
[ExecuteAlways, DisallowMultipleComponent]
public sealed class MotherAppearance : MonoBehaviour
{
    [SerializeField] private MotherAppearanceProfile profile;
    private sealed class Slot
    {
        public Renderer Renderer;
        public Material Material;
        public int Index;
        public MaterialPropertyBlock Original;
    }
    private readonly List<Slot> slots = new List<Slot>();
    private MaterialPropertyBlock workingBlock;
    private MotherAppearanceProfile appliedProfile;
    private Color appliedTint;
    private float appliedSmoothness;
    private bool needsRefresh;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    public MotherAppearanceProfile Profile => profile;

    private void OnEnable() => RefreshAppearance();
    private void OnValidate() => needsRefresh = true;

    private void Update()
    {
        // 공통 프로필을 Inspector에서 바꾸면 모든 활성 엄마가 다음 갱신 때 반영합니다. 보이지 않던 모델은 OnEnable에서 반영합니다.
        if (needsRefresh) RefreshAppearance();
        else if (profile != appliedProfile || (profile != null &&
            (profile.Tint != appliedTint || profile.MaximumSmoothness != appliedSmoothness))) ApplyAppearance();
    }

    [ContextMenu("Refresh Mother Appearance")]
    public void RefreshAppearance()
    {
        RestoreOriginalBlocks();
        slots.Clear();
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                if (materials[index] == null) continue;
                var original = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(original, index);
                slots.Add(new Slot { Renderer = renderer, Material = materials[index], Index = index, Original = original });
            }
        }
        needsRefresh = false;
        ApplyAppearance();
    }

    private void ApplyAppearance()
    {
        appliedProfile = profile;
        if (profile == null) { RestoreOriginalBlocks(); return; }
        appliedTint = profile.Tint;
        appliedSmoothness = profile.MaximumSmoothness;
        if (workingBlock == null) workingBlock = new MaterialPropertyBlock();
        foreach (var slot in slots)
        {
            if (slot.Renderer == null || slot.Material == null) continue;
            workingBlock.Clear();
            slot.Renderer.GetPropertyBlock(workingBlock, slot.Index);
            int colorId = slot.Material.HasProperty(BaseColor) ? BaseColor : ColorProperty;
            if (slot.Material.HasProperty(colorId))
            {
                Color original = slot.Material.GetColor(colorId);
                // 모델 원본의 투명도와 눈썹·속눈썹 알파 클리핑을 유지하며 RGB만 어둡게 합니다.
                workingBlock.SetColor(colorId, new Color(original.r * appliedTint.r, original.g * appliedTint.g,
                    original.b * appliedTint.b, original.a));
            }
            if (slot.Material.HasProperty(Smoothness))
                workingBlock.SetFloat(Smoothness, Mathf.Min(slot.Material.GetFloat(Smoothness), appliedSmoothness));
            if (slot.Material.HasProperty(EmissionColor)) workingBlock.SetColor(EmissionColor, Color.black);
            slot.Renderer.SetPropertyBlock(workingBlock, slot.Index);
        }
    }

    // 비활성화·제거 시 원래 PropertyBlock을 돌려놓아 다른 렌더링 설정에 어두운 색을 남기지 않습니다.
    private void RestoreOriginalBlocks()
    {
        foreach (var slot in slots)
            if (slot.Renderer != null) slot.Renderer.SetPropertyBlock(slot.Original, slot.Index);
    }

    private void OnDisable()
    {
        RestoreOriginalBlocks();
        slots.Clear();
    }
}
