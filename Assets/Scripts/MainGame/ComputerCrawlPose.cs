using UnityEngine;

// 기존 Humanoid의 Idle 자세에서 팔과 다리를 번갈아 뻗습니다. 컴퓨터 등장 동안에만 적용되는 절차적 자세입니다.
internal sealed class ComputerCrawlPose
{
    private readonly struct Limb
    {
        public readonly Transform Upper;
        public readonly Transform Lower;
        public readonly Transform End;

        public Limb(Animator animator, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end)
        {
            Upper = animator.GetBoneTransform(upper);
            Lower = animator.GetBoneTransform(lower);
            End = animator.GetBoneTransform(end);
        }
    }

    private readonly Limb leftArm, rightArm, leftLeg, rightLeg;
    private readonly Transform head;
    private readonly Transform[] bones;
    private readonly Quaternion[] restingRotations;

    public ComputerCrawlPose(Animator animator)
    {
        leftArm = new Limb(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
        rightArm = new Limb(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
        leftLeg = new Limb(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
        rightLeg = new Limb(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        bones = new[] { head, leftArm.Upper, leftArm.Lower, rightArm.Upper, rightArm.Lower,
            leftLeg.Upper, leftLeg.Lower, rightLeg.Upper, rightLeg.Lower };
        restingRotations = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++) restingRotations[i] = bones[i].localRotation;
    }

    public void Apply(Transform root, float elapsed, float weight)
    {
        // 화면 밖에서 Animator 갱신이 생략되어도 이전 프레임의 관절 회전이 누적되지 않게 합니다.
        Restore();
        float stride = 0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.PI * 3.1f);
        PoseArm(root, leftArm, -1f, stride, weight);
        PoseArm(root, rightArm, 1f, 1f - stride, weight);
        PoseLeg(root, leftLeg, -1f, 1f - stride, weight);
        PoseLeg(root, rightLeg, 1f, stride, weight);
        head.rotation = Quaternion.AngleAxis(-58f * weight, root.right) * head.rotation;
    }

    private static void PoseArm(Transform root, Limb arm, float side, float reach, float weight)
    {
        // 누운 상태에서 모델의 로컬 위쪽은 진행 방향이고 로컬 앞쪽은 책상 바닥을 향합니다.
        Aim(root, arm.Upper, arm.Lower, new Vector3(side * 0.35f, 0.12f + 0.74f * reach, 0.82f - 0.52f * reach), weight);
        Aim(root, arm.Lower, arm.End, new Vector3(side * 0.08f, 0.88f, 0.15f + 0.32f * reach), weight);
    }

    private static void PoseLeg(Transform root, Limb leg, float side, float tuck, float weight)
    {
        Aim(root, leg.Upper, leg.Lower, new Vector3(side * (0.08f + 0.35f * tuck), -0.97f + 0.60f * tuck, 0.53f * tuck), weight);
        Aim(root, leg.Lower, leg.End, new Vector3(side * 0.10f, -0.92f, -0.06f - 0.50f * tuck), weight);
    }

    private static void Aim(Transform root, Transform bone, Transform child, Vector3 direction, float weight)
    {
        Quaternion turn = Quaternion.FromToRotation(child.position - bone.position, root.TransformDirection(direction));
        bone.rotation = Quaternion.Slerp(Quaternion.identity, turn, weight) * bone.rotation;
    }

    // 정상 이동으로 돌아가거나 이벤트가 취소될 때 공용 모델의 원래 관절을 복원합니다.
    public void Restore()
    {
        for (int i = 0; i < bones.Length; i++) bones[i].localRotation = restingRotations[i];
    }
}
