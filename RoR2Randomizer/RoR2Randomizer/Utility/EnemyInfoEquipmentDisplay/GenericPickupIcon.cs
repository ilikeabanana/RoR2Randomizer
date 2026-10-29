using RoR2;
using UnityEngine;
using UnityEngine.UI;
using RoR2.UI;
using TMPro;
using UnityEngine.AddressableAssets;
using RoR2Randomizer.Extensions;

namespace RoR2Randomizer.Utility.EnemyInfoEquipmentDisplay
{
    [RequireComponent(typeof(RectTransform))]
    public class GenericPickupIcon : MonoBehaviour
    {
        public static readonly GameObject Prefab;

        static GenericPickupIcon()
        {
            Prefab = Instantiate(Addressables.LoadAssetAsync<GameObject>("RoR2/Base/UI/ItemIcon.prefab").WaitForCompletion());
            DontDestroyOnLoad(Prefab);
            Prefab.hideFlags = HideFlags.HideAndDontSave;
            Prefab.name = "PickupIcon";

            ItemIcon itemIcon = Prefab.GetComponent<ItemIcon>();
            GenericPickupIcon pickupIcon = Prefab.AddComponent<GenericPickupIcon>();

            pickupIcon._glowImage = itemIcon.glowImage;
            pickupIcon._image = itemIcon.image;
            pickupIcon._stackText = itemIcon.stackText;
            pickupIcon._tooltipProvider = itemIcon.tooltipProvider;

            ((RectTransform)pickupIcon.transform).sizeDelta = new Vector2(32f, 32f);

            Destroy(itemIcon);
        }

        [SerializeField]
        RawImage _glowImage;

        [SerializeField]
        RawImage _image;

        [SerializeField]
        TextMeshProUGUI _stackText;

        [SerializeField]
        TooltipProvider _tooltipProvider;

        PickupIndex _pickupIndex;

        int _count;

        public RectTransform rectTransform { get; private set; }

        void Awake()
        {
            CacheRectTransform();
        }

        public void CacheRectTransform()
        {
            if (rectTransform == null)
            {
                rectTransform = (RectTransform)transform;
            }
        }

        public void SetPickupIndex(PickupIndex newPickupIndex, int newPickupCount = 1)
        {
            if (_pickupIndex == newPickupIndex && _count == newPickupCount)
                return;
            
            _pickupIndex = newPickupIndex;
            _count = newPickupCount;

            string titleToken = "";
            string bodyToken = "";
            
            Color titleColor = Color.white;
            Color bodyColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            
            PickupDef pickupDef = PickupCatalog.GetPickupDef(_pickupIndex);
            if (pickupDef != null)
            {
                _image.texture = pickupDef.iconTexture;

                if (_count > 1)
                {
                    _stackText.enabled = true;
                    _stackText.text = $"x{_count}";
                }
                else
                {
                    _stackText.enabled = false;
                }

                titleToken = pickupDef.nameToken;
                titleColor = pickupDef.darkColor;

                bodyToken = "???";
                if (pickupDef.IsItem())
                {
                    ItemDef itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);
                    if (itemDef != null)
                    {
                        bodyToken = itemDef.pickupToken;
                    }
                }
                else if (pickupDef.IsEquipment())
                {
                    EquipmentDef equipmentDef = EquipmentCatalog.GetEquipmentDef(pickupDef.equipmentIndex);
                    if (equipmentDef != null)
                    {
                        bodyToken = equipmentDef.pickupToken;
                    }
                }
            }

            if (_glowImage)
            {
                _glowImage.color = new Color(titleColor.r, titleColor.g, titleColor.b, 0.75f);
            }

            if (_tooltipProvider)
            {
                _tooltipProvider.titleToken = titleToken;
                _tooltipProvider.bodyToken = bodyToken;
                _tooltipProvider.titleColor = titleColor;
                _tooltipProvider.bodyColor = bodyColor;
            }
        }
    }
}
