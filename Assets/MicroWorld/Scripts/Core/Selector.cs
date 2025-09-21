using UnityEngine;

namespace MicroWorldNS
{
    public class Selector : MonoBehaviour
    {
        [SerializeField] GameObject[] Items;

        public void Select(MicroWorld builder)
        {
            if (Items == null || Items.Length == 0)
                return;
            var rnd = new Rnd(builder.Seed + 245);
            var selected = rnd.GetRnd(Items);
            foreach(var item in Items)
                item.SetActive(item == selected);
        }
    }

}
