# 图标生成记录

使用内置 imagegen，每次生成一个独立 PNG；没有生产图集。所有导出均整画布从 1254×1254 最近邻缩放到 96×96，不按主体裁切或调整缩放。

固定参考为项目矿石图标的粗像素语言与第二张生命药水样本。第一张样本未采用。接受样本的生成描述：单个红色生命药瓶、软木塞、黄铜瓶颈，深描边、左上高光、真实透明背景，不含文字、边框、游离光点；全图居中。工具未遵循请求的 1024 画布，因此在扩展前统一锁定实际 1254 画布并修订家族合同，见[资产合同](./asset-contract.md)。

派生提示固定瓶身、位置、像素、光线和透明边距：

- 法力药水：仅将液体改为宝蓝色与青色高光。
- 坚韧药剂：蜂蜜金 / 琥珀液体，在玻璃正面添加小型深色盾牌纹样。
- 净化药剂：浅翡翠绿液体与象牙色高光，玻璃正面添加小型四角星纹样。
- 坚韧状态：以 `status_equipment_guard.png` 为风格和构图参考，深蓝黑不透明底，金铜盾牌与琥珀心形纹样，无文字和边框，完整方图导出。

工具原始样本记录见 [source-audit.json](./source-audit.json)；正式药水与状态尺寸及透明边界分别见 [sprite-audit.json](./sprite-audit.json)、[status-audit.json](./status-audit.json)。状态图标使用有意满幅深色底，因此审计的“可见像素接触画布边缘”警告符合合同。

正式路径：`Assets/Art/Sprites/Items/Consumables/item_*_potion.png` 和 `Assets/Art/Sprites/UI/Statuses/status_fortitude.png`。固定样本保留为本目录的 `health-pilot.png`。
