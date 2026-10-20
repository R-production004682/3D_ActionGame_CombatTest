using System.Collections.Generic;
using UnityEngine;

namespace ActionStudy
{
    /// <summary>
    /// 剣の軌跡。TrailRenderer は「点」の軌跡しか描けないので、
    /// 剣の根元と剣先の 2 点の履歴からリボン状のメッシュを毎フレーム組み立てる。
    /// フレーム間は Catmull-Rom 曲線で補間し、速い振りでも軌跡がカクつかないようにしている。
    /// 時間はスケール時間なので、ヒットストップ中は軌跡もその場で止まる(「斬った瞬間」が残る)。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SwordTrail : MonoBehaviour
    {
        public Transform bladeBase;
        public Transform bladeTip;
        [Tooltip("軌跡が残る時間(秒)")]
        public float lifetime = 0.13f;
        [Tooltip("フレーム間の補間分割数")]
        public int subdivisions = 6;
        public Color color = new Color(0.75f, 0.88f, 1f, 1f);

        struct Sample
        {
            public Vector3 basePos;
            public Vector3 tipPos;
            public float time;
        }

        readonly List<Sample> m_Samples = new List<Sample>();
        readonly List<Vector3> m_Vertices = new List<Vector3>();
        readonly List<Vector2> m_Uvs = new List<Vector2>();
        readonly List<Color> m_Colors = new List<Color>();
        readonly List<int> m_Indices = new List<int>();
        Mesh m_Mesh;

        public bool Emitting { get; set; }

        void Awake()
        {
            m_Mesh = new Mesh { name = "SwordTrail" };
            m_Mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = m_Mesh;
        }

        void OnDestroy()
        {
            if (m_Mesh != null)
                Destroy(m_Mesh);
        }

        void LateUpdate()
        {
            // メッシュはワールド座標で作るので、自身は原点に固定する
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

            float now = Time.time;
            if (Emitting && bladeBase != null && bladeTip != null)
            {
                bool timeAdvanced = m_Samples.Count == 0 || now > m_Samples[m_Samples.Count - 1].time;
                if (timeAdvanced)
                    m_Samples.Add(new Sample { basePos = bladeBase.position, tipPos = bladeTip.position, time = now });
            }

            while (m_Samples.Count > 0 && now - m_Samples[0].time > lifetime)
                m_Samples.RemoveAt(0);

            BuildMesh(now);
        }

        void BuildMesh(float now)
        {
            m_Mesh.Clear();
            if (m_Samples.Count < 2)
                return;

            m_Vertices.Clear();
            m_Uvs.Clear();
            m_Colors.Clear();
            m_Indices.Clear();

            int last = m_Samples.Count - 1;
            for (int i = 0; i < last; i++)
            {
                Sample s0 = m_Samples[Mathf.Max(i - 1, 0)];
                Sample s1 = m_Samples[i];
                Sample s2 = m_Samples[i + 1];
                Sample s3 = m_Samples[Mathf.Min(i + 2, last)];
                int steps = i == last - 1 ? subdivisions + 1 : subdivisions;
                for (int k = 0; k < steps; k++)
                {
                    float u = (float)k / subdivisions;
                    Vector3 b = CatmullRom(s0.basePos, s1.basePos, s2.basePos, s3.basePos, u);
                    Vector3 tip = CatmullRom(s0.tipPos, s1.tipPos, s2.tipPos, s3.tipPos, u);
                    float time = Mathf.Lerp(s1.time, s2.time, u);
                    AddPair(b, tip, Mathf.Clamp01((now - time) / lifetime));
                }
            }

            int pairCount = m_Vertices.Count / 2;
            for (int i = 0; i < pairCount - 1; i++)
            {
                int v = i * 2;
                m_Indices.Add(v);
                m_Indices.Add(v + 1);
                m_Indices.Add(v + 2);
                m_Indices.Add(v + 1);
                m_Indices.Add(v + 3);
                m_Indices.Add(v + 2);
            }

            m_Mesh.SetVertices(m_Vertices);
            m_Mesh.SetUVs(0, m_Uvs);
            m_Mesh.SetColors(m_Colors);
            m_Mesh.SetTriangles(m_Indices, 0);
            m_Mesh.RecalculateBounds();
        }

        void AddPair(Vector3 basePos, Vector3 tipPos, float age01)
        {
            // 古い部分ほど透明に。剣先側を明るく、根元側を細く見せる
            float alpha = Mathf.Pow(1f - age01, 1.6f);
            Vector3 innerPos = Vector3.Lerp(basePos, tipPos, 0.25f + 0.35f * age01);
            Color c = color;
            c.a = alpha;
            m_Vertices.Add(innerPos);
            m_Vertices.Add(tipPos);
            m_Uvs.Add(new Vector2(age01, 0f));
            m_Uvs.Add(new Vector2(age01, 1f));
            m_Colors.Add(c);
            m_Colors.Add(c);
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
