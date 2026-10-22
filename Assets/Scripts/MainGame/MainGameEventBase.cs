using System.Collections;
using UnityEngine;

// 이벤트는 자신의 연출과 발각 가능 구간만 소유합니다. 선택 주기와 게임오버는 Director의 책임입니다.
// 창문 방문·빈 문 열기 등도 이 계약을 상속하면 기존 이벤트 선택 코드를 수정할 필요가 없습니다.
public abstract class MainGameEventBase : MonoBehaviour
{
    private Coroutine sequence;
    public bool IsRunning { get; private set; }
    public abstract bool CanRun { get; }
    public abstract bool IsDetectionActive { get; }

    // Inspector에 등록된 유효한 이벤트만 Director가 시작합니다. 같은 이벤트의 중복 실행은 막습니다.
    public bool TryBegin()
    {
        if (!isActiveAndEnabled || IsRunning || !CanRun) return false;
        IsRunning = true;
        sequence = StartCoroutine(Run());
        return true;
    }

    private IEnumerator Run()
    {
        yield return ExecuteEvent();
        IsRunning = false;
        sequence = null;
    }

    protected abstract IEnumerator ExecuteEvent();

    // 게임오버·씬 종료에서 호출합니다. 파생 이벤트는 소리와 배우 등 자신이 사용한 자원도 정리해야 합니다.
    public void Cancel()
    {
        if (!IsRunning) return;
        IsRunning = false;
        if (sequence != null) StopCoroutine(sequence);
        sequence = null;
        OnCancelled();
    }

    protected abstract void OnCancelled();
    protected virtual void OnDisable() => Cancel();
}
