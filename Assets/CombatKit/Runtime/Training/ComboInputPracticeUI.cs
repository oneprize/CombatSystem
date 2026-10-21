using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using CombatSystem.Player;
namespace CombatSystem.Training
{
    /// <summary>
    /// 콤보 훈련 구역 안에서 4개 레슨(지상 콤보 / 띄우기 연계 공중 콤보 / E 연계 / R 연계)의 입력 순서를 아이콘으로 안내하고,
    /// 플레이어의 실제 공격을 관찰해 순서대로 성공할 때마다 진행도를 올린다. 레슨을 완료하면 잠시 뒤 다음 레슨으로 넘어간다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class ComboInputPracticeUI : MonoBehaviour
    {
        #region 필드

        public PlayerCharacter player;
        public CanvasGroup panel;
        public Image[] icons;
        public Image[] advancedIcons;
        public Image[] skillIcons;
        public GameObject skillRow;
        public Text skillKeyLabel;
        public GameObject basicRow, advancedRow;
        public Text titleLabel, progressLabel;
        public Vector3 arenaCenter;
        /// <summary>훈련 구역 중심 기준 X/Z 절반 크기. 플레이어가 이 안에 있을 때만 패널을 표시한다.</summary>
        public Vector2 arenaHalfSize = new Vector2(15,15);
        /// <summary>레슨 완료 후 다음 레슨으로 넘어가기까지 대기 시간(초).</summary>
        public float restartDelay = 1.5f;
        /// <summary>단계 사이에 허용되는 최대 간격(초). 넘기면 진행도를 처음부터 다시 센다.</summary>
        public float chainWaitLimit = 5f;
        private int lesson, completed, lastAttackSequence = -1;
        private float restartAt = -1, lastStepAt;
        private bool wasInside;
        /// <summary>현재 레슨에 해당하는 입력 아이콘 줄. 레슨 2·3은 같은 스킬 아이콘 줄을 공유한다.</summary>
        private Image[] CurrentIcons => lesson == 0 ? icons : lesson == 1 ? advancedIcons : skillIcons;

        #endregion

        #region 공격 관찰

        /// <summary>
        /// 플레이어가 구역 안에 있는지 확인해 패널을 켜고 끈다. 진입 시 진행도를 초기화하고, 레슨 완료 대기가 끝나면 다음 레슨으로 넘긴다.
        /// 그 외에는 현재 공격 종류와 콤보 단계를 판별해 ObserveAttack에 넘긴다(전투 처리 후에 읽도록 늦은 실행 순서를 쓴다).
        /// </summary>
        private void LateUpdate()
        {
            if (player == null) player = FindFirstObjectByType<PlayerCharacter>();
            bool inside = player != null && !player.IsDead &&
                Mathf.Abs(player.transform.position.x-arenaCenter.x)<arenaHalfSize.x &&
                Mathf.Abs(player.transform.position.z-arenaCenter.z)<arenaHalfSize.y;
            panel.alpha = inside ? 1 : 0;
            if (!inside) { wasInside=false; return; }
            if (!wasInside) { ResetPractice(); wasInside=true; }
            if (Time.timeScale == 0) return;
            if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
            { ResetPractice(); return; }
            if (restartAt >= 0)
            {
                if (Time.time >= restartAt)
                {
                    lesson=(lesson+1)%4;
                    ResetPractice();
                }
                return;
            }
            var c=player.Combat;
            if (c==null) return;
            int kind=player.AirCombo.IsAirDashing?9:c.IsUsingGroundCombo?1:c.IsUsingSkillC?2:c.IsUsingAirCombo?3:c.IsUsingChaseAttack?4:c.IsUsingSkillA?6:c.IsUsingSkillB?7:c.IsUsingEFollowUp?8:c.IsAttacking?5:0;
            ObserveAttack(c.AttackSequence,kind,c.IsUsingAirCombo?c.AirComboIndex:c.AttackComboIndex);
        }

        /// <summary>
        /// 새 공격 시퀀스가 시작될 때마다 현재 레슨의 기대 순서와 비교해 맞으면 진행도를 올리고, 틀리면 처음부터 다시 시작하게 한다.
        /// kind 값: 0 대기, 1 지상 콤보, 2 띄우기(SkillC), 3 공중 콤보, 4 추격 공격, 5 기타 공격, 6 SkillA(E), 7 SkillB(R), 8 E 후속타, 9 공중 대시.
        /// </summary>
        private void ObserveAttack(int sequence,int kind,int step)
        {
            if (restartAt>=0) return;
            if (completed>0 && Time.time-lastStepAt>chainWaitLimit) ClearProgress();
            if (kind==0)
            {
                // 모든 레슨은 공격이 끊기지 않고 이어져야 하므로, 대기 상태로 돌아오면 진행도를 초기화한다.
                ClearProgress();
                lastAttackSequence=sequence;
                return;
            }
            if (sequence==lastAttackSequence) return;
            lastAttackSequence=sequence;
            if (lesson==0)
            {
                if (kind!=1) { ClearProgress(); return; }
                if (step==0) ClearProgress();
                if (step!=completed) { ClearProgress(); return; }
            }
            else if (lesson==1)
            {
                if (kind==2) { ClearProgress(); Advance(); return; }
                bool valid=completed==1 && kind==4;
                valid |= completed>=2 && completed<=4 && kind==3 && step==completed-2;
                valid |= completed==5 && kind==9;
                if (!valid) { ClearProgress(); return; }
            }
            else
            {
                int expectedSkill=lesson==2?6:7;
                if(kind==expectedSkill) { ClearProgress(); Advance(); return; }
                bool valid=completed==1 && kind==8;
                valid |= completed>=2 && completed<=5 && kind==1 && step==completed-2;
                if(!valid) { ClearProgress(); return; }
            }
            Advance();
        }

        #endregion

        #region 진행도 표시

        /// <summary>성공한 단계의 아이콘을 숨기고 진행도 텍스트를 갱신한다. 마지막 단계면 다음 레슨 전환을 예약한다.</summary>
        private void Advance()
        {
            CurrentIcons[completed++].gameObject.SetActive(false);
            lastStepAt=Time.time;
            progressLabel.text=completed==CurrentIcons.Length?"COMPLETE!":completed+" / "+CurrentIcons.Length;
            if (completed==CurrentIcons.Length) restartAt=Time.time+restartDelay;
        }
        /// <summary>현재 레슨의 진행도를 0으로 되돌리고 모든 아이콘을 다시 표시한다.</summary>
        private void ClearProgress()
        {
            completed=0; restartAt=-1;
            foreach(var icon in CurrentIcons) icon.gameObject.SetActive(true);
            progressLabel.text="0 / "+CurrentIcons.Length;
        }
        /// <summary>
        /// 현재 레슨에 맞는 아이콘 줄·키 라벨·제목을 표시하고 진행도를 초기화한다.
        /// 진행 중이던 공격 시퀀스가 새 단계로 잘못 집계되지 않도록 현재 시퀀스 번호를 기준점으로 저장한다.
        /// </summary>
        public void ResetPractice()
        {
            if(basicRow!=null) basicRow.SetActive(lesson==0);
            if(advancedRow!=null) advancedRow.SetActive(lesson==1);
            if(skillRow!=null) skillRow.SetActive(lesson>=2);
            if(skillKeyLabel!=null) skillKeyLabel.text=lesson==2?"E":"R";
            if(titleLabel!=null) titleLabel.text=lesson==0?"01 / GROUND COMBO":lesson==1?"02 / LAUNCH > CHASE > AIR 1-3 > E":lesson==2?"03 / E > FOLLOW-UP > GROUND COMBO":"04 / R > FOLLOW-UP > GROUND COMBO";
            ClearProgress();
            lastAttackSequence=player!=null && player.Combat!=null?player.Combat.AttackSequence:-1;
        }

        #endregion
    }
}