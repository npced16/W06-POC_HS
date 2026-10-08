using System.Linq;
using UnityEngine;

namespace WhistlePOC
{
    public sealed class DuelMap : MonoBehaviour
    {
        public void Bind(BossDuel game)
        {
            game.walls = GetComponentsInChildren<DuelMapModule>().SelectMany(module => module.navigationObstacles)
                .Where(obstacle => obstacle != null && obstacle.enabled && obstacle.gameObject.activeInHierarchy).Distinct().ToArray();
            game.building = GetComponentsInChildren<MeshRenderer>();
            game.buildingContours = GetComponentsInChildren<LineRenderer>();
        }
    }
}
