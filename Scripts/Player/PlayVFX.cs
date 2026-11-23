using System.Collections.Generic;
using UnityEngine;

public enum ParticleType
{
    LevelUp,
    GetRunner,
}

public class PlayVFX : MonoBehaviour
{
    [System.Serializable]
    private struct ParticleEntry
    {
        public ParticleType type;
        public ParticleSystem particle;
    }

    [SerializeField] private ParticleEntry[] entries;

    private readonly Dictionary<ParticleType, ParticleSystem> particleMap = new();

    private void Awake()
    {
        foreach (var entry in entries)
        {
            // ParticleSystem.main
            // 인스펙터의 ParticleSystem 맨 위 섹션(Duration, Looping, Start Lifetime 등)에 해당하는 Main 모듈이다.
            // struct지만 내부적으로 실제 파티클을 가리키는 핸들이라서,
            // 지역 변수에 받아 값을 바꿔도 원본 파티클에 바로 반영된다. (다시 대입할 필요 없음)
            var main = entry.particle.main;

            // MainModule.loop
            // 인스펙터의 Looping 체크박스다. true면 Duration이 끝나도 처음부터 계속 반복한다.
            // false면 Duration 동안만 파티클을 뿜고, 남은 파티클이 수명(Start Lifetime)을 다하면 재생이 끝난다.
            main.loop = false;

            // MainModule.stopAction
            // 인스펙터의 Stop Action 항목이다. 재생이 완전히 끝났을 때(모든 파티클이 사라졌을 때) 할 일을 정한다.
            // None: 아무것도 안 함 / Disable: gameObject를 SetActive(false) / Destroy: gameObject 삭제 / Callback: OnParticleSystemStopped 호출
            // 루프 중에는 재생이 끝나지 않으므로 Stop Action도 실행되지 않는다.
            main.stopAction = ParticleSystemStopAction.Disable;

            particleMap[entry.type] = entry.particle;
            entry.particle.gameObject.SetActive(false);
        }
    }

    public void PlayParticle(ParticleType particleType, float scale = 1f)
    {
        if (particleMap.TryGetValue(particleType, out var particle) == false)
        {
            Debug.LogWarning($"[PlayVFX] {particleType} 파티클이 등록되지 않았습니다.", this);
            return;
        }

        particle.transform.localScale = Vector3.one * scale;
        
        particle.gameObject.SetActive(true);

        // ParticleSystem.Stop(withChildren, stopBehavior)
        // 재생을 멈춘다. 첫 번째 인자가 true면 자식 오브젝트의 ParticleSystem도 같이 멈춘다.
        // stopBehavior가 StopEmitting이면 새 파티클만 안 나오고 이미 나온 파티클은 수명대로 남는다.
        // StopEmittingAndClear면 이미 나온 파티클까지 즉시 전부 지운다.
        // Play()는 이미 재생 중이면 아무 일도 하지 않으므로, 연속 호출 시 처음부터 다시 재생하려고 먼저 멈춘다.
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // ParticleSystem.Play(withChildren)
        // 재생을 시작한다. 멈춘 상태에서 호출하면 재생 시간(Duration)을 0부터 다시 센다.
        // true를 넘기면 자식 오브젝트의 ParticleSystem도 같이 재생한다.
        particle.Play(true);
    }
}
