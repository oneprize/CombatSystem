using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using CombatSystem.Player;
namespace CombatSystem.Training
{
    [DefaultExecutionOrder(100)]
    public sealed class ComboInputPracticeUI : MonoBehaviour
    {
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
        public Vector2 arenaHalfSize = new Vector2(15,15);
        public float restartDelay = 1.5f;
        public float chainWaitLimit = 5f;
        private int lesson, completed, lastAttackSequence = -1;
        private float restartAt = -1, lastStepAt;
        private bool wasInside;
        private Image[] CurrentIcons => lesson == 0 ? icons : lesson == 1 ? advancedIcons : skillIcons;

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

        // Kinds: idle, ground combo, launcher, air combo, chase, other attack.
        private void ObserveAttack(int sequence,int kind,int step)
        {
            if (restartAt>=0) return;
            if (completed>0 && Time.time-lastStepAt>chainWaitLimit) ClearProgress();
            if (kind==0)
            {
                // Every lesson now requires an uninterrupted attack sequence.
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
        private void Advance()
        {
            CurrentIcons[completed++].gameObject.SetActive(false);
            lastStepAt=Time.time;
            progressLabel.text=completed==CurrentIcons.Length?"COMPLETE!":completed+" / "+CurrentIcons.Length;
            if (completed==CurrentIcons.Length) restartAt=Time.time+restartDelay;
        }
        private void ClearProgress()
        {
            completed=0; restartAt=-1;
            foreach(var icon in CurrentIcons) icon.gameObject.SetActive(true);
            progressLabel.text="0 / "+CurrentIcons.Length;
        }
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
    }
}