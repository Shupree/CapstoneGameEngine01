using UnityEngine;

// 모든 엄마 프리팹과 이벤트가 같은 외형 설정을 참조합니다. 원본 텍스처·공유 머티리얼을 덮어쓰지 않습니다.
[CreateAssetMenu(menuName = "MainGame/Mother Appearance Profile")]
public sealed class MotherAppearanceProfile : ScriptableObject
{
    [Tooltip("원래 피부·머리·옷 색에 곱할 색입니다. RGB가 낮을수록 검정에 가까워지며 알파 클리핑은 유지됩니다.")]
    [SerializeField, ColorUsage(false, false)] private Color tint = new Color(0.10f, 0.10f, 0.10f, 1f);
    [Tooltip("피부·눈·옷의 최대 광택입니다. 밝은 반사가 실루엣을 방해하지 않도록 제한합니다.")]
    [SerializeField, Range(0f, 1f)] private float maximumSmoothness = 0.08f;

    public Color Tint => tint;
    public float MaximumSmoothness => maximumSmoothness;

    private void OnValidate()
    {
        // 투명도는 모델의 기존 머티리얼이 소유합니다. 외형 프로필은 밝기와 광택만 조정합니다.
        tint = new Color(Mathf.Clamp01(tint.r), Mathf.Clamp01(tint.g), Mathf.Clamp01(tint.b), 1f);
        maximumSmoothness = Mathf.Clamp01(maximumSmoothness);
    }
}
