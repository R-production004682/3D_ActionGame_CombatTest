using System.Collections.Generic;
using UnityEngine;

namespace ActionStudy
{
    /// <summary>
    /// かかしを決まった位置(スロット)に並べるスポナー。倒すと少し待ってから、同じスロットに立て直す。
    /// スロットは開始地点を中心に円形に並べ、どれも中心(プレイヤーの開始位置)を向かせる。
    /// 既定は 1 体で、プレイヤーの開始時の正面 (+Z) に置く。
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        public Enemy enemyPrefab;
        public Transform player;
        [Tooltip("並べる数")]
        public int aliveCount = 1;
        public float respawnDelay = 1.2f;
        [Tooltip("並べる円の半径 (m)")]
        public float slotRadius = 4f;
        [Tooltip("1 体目を置く方向 (度)。0 = +Z (プレイヤーの開始時の正面)")]
        public float slotStartAngle;
        [Tooltip("並べる円の中心")]
        public Vector3 slotCenter = Vector3.zero;
        [Tooltip("ON にすると、かかしではなくプレイヤーへ寄ってくる敵になる")]
        public bool chasePlayer;

        readonly Dictionary<Enemy, int> m_SlotOf = new Dictionary<Enemy, int>();
        readonly Queue<int> m_PendingSlots = new Queue<int>();
        float m_NextSpawnTime;

        void Start()
        {
            for (int i = 0; i < aliveCount; i++)
                Spawn(i);
        }

        void Update()
        {
            if (m_PendingSlots.Count > 0 && Time.time >= m_NextSpawnTime)
            {
                m_NextSpawnTime = Time.time + 0.4f;
                Spawn(m_PendingSlots.Dequeue());
            }
        }

        Vector3 SlotPosition(int slot)
        {
            float angle = slotStartAngle + (float)slot / Mathf.Max(1, aliveCount) * 360f;
            Vector3 position = slotCenter + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * slotRadius;
            position.y = 0f;
            return position;
        }

        void Spawn(int slot)
        {
            if (enemyPrefab == null)
                return;
            Vector3 position = SlotPosition(slot);
            Vector3 toCenter = Ease.Flat(slotCenter - position);
            Quaternion rotation = Quaternion.LookRotation(toCenter.sqrMagnitude > 1e-4f ? toCenter.normalized : Vector3.forward);

            Enemy enemy = Instantiate(enemyPrefab, position, rotation);
            enemy.name = enemyPrefab.name;
            enemy.chasePlayer = chasePlayer;
            enemy.SetHome(position, rotation);
            enemy.SetPlayer(player);
            enemy.Died += OnEnemyDied;
            m_SlotOf[enemy] = slot;
        }

        void OnEnemyDied(Enemy enemy)
        {
            if (m_SlotOf.TryGetValue(enemy, out int slot))
            {
                m_SlotOf.Remove(enemy);
                m_PendingSlots.Enqueue(slot);
            }
            m_NextSpawnTime = Mathf.Max(m_NextSpawnTime, Time.time + respawnDelay);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.7f, 0.3f, 0.8f);
            for (int i = 0; i < aliveCount; i++)
                Gizmos.DrawWireSphere(SlotPosition(i) + Vector3.up, 0.5f);
        }
    }
}
