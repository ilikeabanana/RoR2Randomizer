using RoR2;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoR2Randomizer.Utility.EnemyInfoEquipmentDisplay
{
    [RequireComponent(typeof(RectTransform))]
    public class EquipmentInventoryDisplay : UIBehaviour, ILayoutElement
    {
        const float EQUIPMENT_ICON_PREFAB_WIDTH = 32f;

        const float MAX_ICON_WIDTH = 32f;

        const float MAX_HEIGHT = 256f;

        const float VERTICAL_MARGIN = 8f;

        RectTransform _rectTransform;

        readonly List<GenericPickupIcon> _equipmentIcons = new List<GenericPickupIcon>();

        EquipmentIndex[] _equipments;

        float _currentIconScale = 1f;

        float _previousWidth;

        bool _updateRequestPending;

        Inventory _inventory;

        bool _inventoryWasValid;

        bool isUninitialized => _rectTransform == null;

        public float minWidth => preferredWidth;

        public float preferredWidth => _rectTransform.rect.width;

        public float flexibleWidth => 0f;

        public float minHeight => preferredHeight;

        public float preferredHeight { get; set; }

        public float flexibleHeight => 0f;

        public int layoutPriority => 0;

        public void SetSubscribedInventory(Inventory newInventory)
        {
            if (_inventory == newInventory && _inventory == _inventoryWasValid)
                return;
            
            if (_inventory != null)
            {
                _inventory.onInventoryChanged -= onInventoryChanged;
                _inventory = null;
            }

            _inventory = newInventory;
            _inventoryWasValid = _inventory;

            if (_inventory)
                _inventory.onInventoryChanged += onInventoryChanged;

            onInventoryChanged();
        }

        void onInventoryChanged()
        {
            if (!isActiveAndEnabled)
                return;
            
            if (_inventory)
            {
                int equipmentSlotCount = _inventory.GetEquipmentSlotCount();
                _equipments = new EquipmentIndex[equipmentSlotCount];

                for (uint i = 0; i < equipmentSlotCount; i++)
                {
                    _equipments[i] = _inventory.GetEquipment(i).equipmentIndex;
                }
            }
            else
            {
                Array.Clear(_equipments, 0, _equipments.Length);
            }

            requestUpdateDisplay();
        }

        void allocateIcons(int desiredItemCount)
        {
            if (desiredItemCount != _equipmentIcons.Count)
            {
                while (_equipmentIcons.Count > desiredItemCount)
                {
                    Destroy(_equipmentIcons[_equipmentIcons.Count - 1].gameObject);
                    _equipmentIcons.RemoveAt(_equipmentIcons.Count - 1);
                }

                calculateLayoutValues(out LayoutValues layoutValues, desiredItemCount);
                while (_equipmentIcons.Count < desiredItemCount)
                {
                    GenericPickupIcon icon = Instantiate(GenericPickupIcon.Prefab, _rectTransform).GetComponent<GenericPickupIcon>();
                    _equipmentIcons.Add(icon);
                    layoutIndividualIcon(layoutValues, _equipmentIcons.Count - 1);
                }
            }

            onIconCountChanged();
        }

        float calculateIconScale(int iconCount)
        {
            int containerWidth = (int)_rectTransform.rect.width;
            int maxHeight = (int)MAX_HEIGHT;
            int iconPrefabWidth = (int)EQUIPMENT_ICON_PREFAB_WIDTH;
            int currentIconWidth = iconPrefabWidth;
            int minimumIconWidth = iconPrefabWidth / 8;
            int maxIconsPerRow = Math.Max(containerWidth / currentIconWidth, 1);
            int totalRows = HGMath.IntDivCeil(iconCount, maxIconsPerRow);

            while (currentIconWidth * totalRows > maxHeight)
            {
                maxIconsPerRow++;
                currentIconWidth = Math.Min(containerWidth / maxIconsPerRow, iconPrefabWidth);
                totalRows = HGMath.IntDivCeil(iconCount, maxIconsPerRow);

                if (currentIconWidth <= minimumIconWidth)
                {
                    currentIconWidth = minimumIconWidth;
                    break;
                }
            }

            currentIconWidth = Math.Min(currentIconWidth, (int)MAX_ICON_WIDTH);

            return currentIconWidth / (float)iconPrefabWidth;
        }

        void onIconCountChanged()
        {
            float iconScale = calculateIconScale(_equipmentIcons.Count);
            if (iconScale != _currentIconScale)
            {
                _currentIconScale = iconScale;
                onIconScaleChanged();
            }
        }

        void onIconScaleChanged()
        {
            layoutAllIcons();
        }

        void calculateLayoutValues(out LayoutValues v, int iconCount)
        {
            float iconScale = calculateIconScale(_equipmentIcons.Count);
            Rect containerRect = _rectTransform.rect;

            v.Width = containerRect.width;
            v.IconSize = EQUIPMENT_ICON_PREFAB_WIDTH * iconScale;
            v.IconsPerRow = Math.Max((int)v.Width / (int)v.IconSize, 1);
            v.RowWidth = v.IconsPerRow * v.IconSize;

            float horizontalMargin = (v.Width - v.RowWidth) * 0.5f;

            v.RowCount = HGMath.IntDivCeil(_equipmentIcons.Count, v.IconsPerRow);
            v.IconLocalScale = new Vector3(iconScale, iconScale, 1f);
            v.TopLeftCorner = new Vector3(containerRect.xMin + horizontalMargin, containerRect.yMax - VERTICAL_MARGIN);
            v.Height = (v.IconSize * v.RowCount) + (VERTICAL_MARGIN * 2f);
        }

        void layoutAllIcons()
        {
            calculateLayoutValues(out LayoutValues layoutValues, _equipmentIcons.Count);

            int remainingIcons = _equipmentIcons.Count - ((layoutValues.RowCount - 1) * layoutValues.IconsPerRow);

            int rowIndex = 0;
            int iconIndex = 0;
            while (rowIndex < layoutValues.RowCount)
            {
                Vector3 topLeftCorner = layoutValues.TopLeftCorner;
                topLeftCorner.y += rowIndex * -layoutValues.IconSize;

                int iconsInRow = layoutValues.IconsPerRow;

                if (rowIndex == layoutValues.RowCount - 1)
                    iconsInRow = remainingIcons;
                
                int columnIndex = 0;
                while (columnIndex < iconsInRow)
                {
                    RectTransform rectTransform = _equipmentIcons[iconIndex].rectTransform;
                    rectTransform.localScale = layoutValues.IconLocalScale;
                    rectTransform.localPosition = topLeftCorner;

                    topLeftCorner.x += layoutValues.IconSize;

                    columnIndex++;
                    iconIndex++;
                }

                rowIndex++;
            }
        }

        void layoutIndividualIcon(in LayoutValues layoutValues, int i)
        {
            int rowIndex = i / layoutValues.IconsPerRow;
            int columnIndex = i - (rowIndex * layoutValues.IconsPerRow);

            Vector3 topLeftCorner = layoutValues.TopLeftCorner;
            topLeftCorner.x += columnIndex * layoutValues.IconSize;
            topLeftCorner.y += rowIndex * -layoutValues.IconSize;

            RectTransform rectTransform = _equipmentIcons[i].rectTransform;
            rectTransform.localPosition = topLeftCorner;
            rectTransform.localScale = layoutValues.IconLocalScale;
        }

        void cacheComponents()
        {
            _rectTransform = (RectTransform)transform;
        }

        protected override void Awake()
        {
            base.Awake();
            cacheComponents();
            _equipments = Array.Empty<EquipmentIndex>();
        }

        protected override void OnDestroy()
        {
            SetSubscribedInventory(null);
            base.OnDestroy();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_inventory)
            {
                onInventoryChanged();
            }

            requestUpdateDisplay();
            layoutAllIcons();
        }

        void requestUpdateDisplay()
        {
            if (!_updateRequestPending)
            {
                _updateRequestPending = true;
                RoR2Application.onNextUpdate += UpdateDisplay;
            }
        }

        public void UpdateDisplay()
        {
            _updateRequestPending = false;
            if (!this || !isActiveAndEnabled)
                return;

            allocateIcons(_equipments.Length);

            for (int i = 0; i < _equipments.Length; i++)
            {
                _equipmentIcons[i].SetPickupIndex(PickupCatalog.FindPickupIndex(_equipments[i]));
            }
        }

        public void SetEquipments(EquipmentIndex[] newEquipments)
        {
            _equipments = newEquipments;
            requestUpdateDisplay();
        }

        public void ResetEquipments()
        {
            Array.Clear(_equipments, 0, _equipments.Length);
            requestUpdateDisplay();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            if (_rectTransform)
            {
                float width = _rectTransform.rect.width;
                if (width != _previousWidth)
                {
                    _previousWidth = width;
                    layoutAllIcons();
                }
            }
        }

        public void CalculateLayoutInputHorizontal()
        {
        }

        public void CalculateLayoutInputVertical()
        {
            if (isUninitialized)
                return;

            calculateLayoutValues(out LayoutValues layoutValues, _equipmentIcons.Count);
            preferredHeight = layoutValues.Height;
        }

        protected void OnValidate()
        {
            cacheComponents();
        }

        struct LayoutValues
        {
            public float Width;

            public float Height;

            public float IconSize;

            public int IconsPerRow;

            public float RowWidth;

            public int RowCount;

            public Vector3 IconLocalScale;

            public Vector3 TopLeftCorner;
        }
    }
}
