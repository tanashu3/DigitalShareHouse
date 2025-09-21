using MicroWorldNS.Spawners;
using System;
using System.Collections;
using UnityEngine;

namespace MicroWorldNS
{
    [ExecuteAlways]
    [RequireComponent(typeof(Terrain))]
    class AdjustObjectsPosition : MonoBehaviour
    {
        public float DelayTime = 0.3f;

        float lastChangedTime = 0;
        Terrain terrain;

        private void Update()
        {
            if (lastChangedTime > 0 && lastChangedTime + DelayTime < Time.time)
            {
                lastChangedTime = 0;
                if (Application.isPlaying)
                {
                    StartCoroutine(UpdatePositions());
                }
                else
                {
                    var en = UpdatePositions();
                    while (en.MoveNext()) ;
                }
            }
        }

        void OnTerrainChanged(TerrainChangedFlags flags)
        {
            if ((flags & (TerrainChangedFlags.Heightmap | TerrainChangedFlags.DelayedHeightmapUpdate)) != 0)
                lastChangedTime = Time.time;
        }

        private IEnumerator UpdatePositions()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            terrain = GetComponent<Terrain>();
            if (terrain == null)
                yield break;

            var myBuilder = GetComponentInParent<ILinkToMicroWorld>()?.MicroWorld;
            if (myBuilder == null)
                yield break;

            // process all root objects that link to my builder
            foreach (var holder in MicroWorldHelper.GetRootObjects<LinkToMicroWorld>())
            if (holder.MicroWorld == myBuilder && holder != this.transform)
            foreach (var _ in UpdatePositions(holder.transform, sw))
                    yield return null;

            // process my children
            foreach (var _ in UpdatePositions(this.transform, sw))
                yield return null;
        }

        private IEnumerable UpdatePositions(Transform holder, System.Diagnostics.Stopwatch sw)
        {
            for (int i = 0; i < holder.childCount; i++)
            {
                var info = holder.GetChild(i).GetComponent<SpawnedObjInfo>();
                if (info)
                    UpdatePosition(info);

                if (sw.ElapsedMilliseconds > Preferences.Instance.MaxBuildDutyPerFrameInMs)
                {
                    yield return null;
                    sw.Restart();
                }
            }
        }

        private void UpdatePosition(SpawnedObjInfo info)
        {
            var pos = info.transform.position;
            pos.y = terrain.SampleHeight(pos) + info.OffsetY;
            info.transform.position = pos;
        }
    }
}
