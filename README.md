1. Add Basemap from ArcGIS Online
2. Load the 'State Agency Lands' layer from a Shapefile
3. Display points using a GraphicsOverlay
4. Add annotations via right-click interaction
5. Export annotation data for persistence or sharing

# 2025-8-22-23学习成果： 深入理解了 arcGIS 系统核心概念整理

## 1️⃣ SDK 核心对象

| 对象类型 | 说明 |
| :--- | :--- |
| **Map** | 二维地图容器，地图画布，承载图层（如 FeatureLayer、RasterLayer），管理二维视图和空间参考。 |
| **Scene** | 三维地图容器，三维视图画布，支持地形、3D Tiles、Mesh 等，管理三维场景和相机视角。 |
| **Layer** | 图层（FeatureLayer, RasterLayer, SceneLayer 等），数据可视化载体，控制显示属性（如符号、透明度），不直接修改底层数据。 |
| **Geodatabase** | 地理数据库，管理文件、企业或移动地理数据库，存储要素类、表和关系。 |
| **FeatureClass** | 要素类，存储一组要素（点、线、面），包含几何和属性表，位于地理数据库中。 |
| **Feature** | 单个要素，表示一个地理对象，包含几何（Geometry）和属性（字段值）。 |
| **Geometry** | 几何对象（Point, Polyline, Polygon 等），描述要素的空间形状，支持空间分析和可视化。 |
| **Table** | 非空间表，存储属性数据，无几何信息，可与要素类关联。 |
| **Row** | 表行，表示表或要素类中的一行数据（要素或非空间记录）。 |
| **QueryFilter** | 查询过滤器，定义查询条件，用于筛选要素或表行。 |
| **Selection** | 选择集，管理查询或用户选择的结果集，支持要素高亮和操作。 |
| **EditOperation** | 编辑操作，控制数据编辑（插入、更新、删除），支持事务管理和撤销。 |

---

### 1.1 Layer 可修改属性（仅影响显示）

| 属性   | 方法示例                                                        | 说明          |
| ---- | ----------------------------------------------------------- | ----------- |
| 可见性  | `layer.setVisible(true/false)`                              | 控制图层显示与隐藏   |
| 渲染方式 | `featureLayer.setRenderer(renderer)`                        | 改变符号化、颜色、样式 |
| 透明度  | `layer.setOpacity(0.5)`                                     | 控制图层半透明效果   |
| 定义查询 | `featureLayer.setDefinitionExpression("TYPE='GasStation'")` | 过滤显示的要素子集   |
| 图层顺序 | 在 `map.getOperationalLayers()` 中调整位置                        | 仅影响显示顺序     |
常用操作：
* `SetDefinitionExpression("POPULATION > 1000")` → 过滤显示要素
* `SelectFeaturesAsync()` → 选中要素
* `ClearSelection()` → 清除选择
* `FeatureLayer.Symbology` → 改变显示符号

> ⚠️ 这些操作 **不会修改底层数据**，仅改变地图或场景的显示效果。


---

### 1.2 Layer **不能直接修改的数据**

| 类型          | 说明                                                    |
| ----------- | ----------------------------------------------------- |
| 几何与属性       | 无法直接修改 feature 的坐标或字段值                                |
| 存储规则 / 网络结构 | 如 Utility Network 规则，必须通过 FeatureTable/Network API 操作 |
| 服务端图层       | 如果 Layer 来自只读服务，则客户端无法修改数据                            |

---

### 1.3 修改数据的正确流程

1. 获取 **FeatureTable**

   ```csharp
   var table = featureLayer.GetFeatureTable();
   ```
2. 查询需要修改的 **Feature**
3. 修改 **geometry** 或 **attribute**
4. 提交修改：

   ```csharp
   await table.UpdateFeatureAsync(feature);
   await table.ApplyEditsAsync();
   ```

> 仅当 FeatureTable 可写（编辑服务或本地 Geodatabase）时，修改才会持久化。

---

### 1.4 总结

| 层级      | 对象                      | 可改内容          | 持久化 |
| ------- | ----------------------- | ------------- | --- |
| **视图层** | Layer / GraphicsOverlay | 显示属性、可见性、过滤条件 | 否   |
| **数据层** | Table / FeatureTable    | 几何、属性、CRUD 操作 | 是   |
| **临时层** | GraphicsOverlay         | 绘制标注、线、点等     | 否   |

> 类比：`Map/Scene = 画布`，`Layer/GraphicsOverlay = 画在上面的内容`

---

## 2️⃣ 数据来源

| 类型                  | 说明               | 特点                                       |
| ------------------- | ---------------- | ---------------------------------------- |
| **Feature（要素）**     | 点/线/面 + 属性       | 来自 FeatureClass（数据库表），可编辑或只读             |
| **Raster（栅格）**      | 像素矩阵，每个 cell 存储值 | 影像（卫星、航拍）、DEM、高程、土地利用等                   |
| **Mesh / 3D Tiles** | 三维网格模型（点 + 面）    | BIM/CAD/无人机生成，真实表达三维地物，通常只读，部分带 metadata |

> 类比：
> Raster = Excel 表格，每个格子有值
> Mesh = 3D 模型，用点和面拼成真实表面

---

### 2.1 SDK 加载数据源方式

| 数据源             | 说明                                                                                        |
| --------------- | ----------------------------------------------------------------------------------------- |
| **Geodatabase** | 本地或企业级，支持 FeatureClass、Table、规则、拓扑等                                                       |
| **Web 服务**      | ArcGIS Server / ArcGIS Online → Feature Service, Map Service, Scene Service, Tile Service |
| **本地文件**        | Shapefile, GeoJSON, Mobile Geodatabase (.geodatabase), Raster (.tif)                      |
| **配置文件**        | WebMap (2D JSON), WebScene (3D JSON) → 描述图层、样式、可见性                                        |
| **Style 文件**    | 存储符号化信息                                                                                   |

---

## 3️⃣ Utility Network（公用事业网络），arcGIS 已经内置了一套 Utility Network 要素和规则

| 组成                             | 类型      | 说明                        |
| ------------------------------ | ------- | ------------------------- |
| Valve, Pipe, Pump, Transformer | Feature | 带几何 + 属性，存储在 FeatureClass |
| 拓扑 (Topology)                  | 数据表     | 描述谁连谁的网络关系                |
| 规则 (Rules)                     | 数据表     | 如 “6 寸管不能直接接 2 寸阀门”       |
| 关联表 (Associations)             | 数据表     | 非几何逻辑关系（泵站与电源）            |

在 Runtime SDK 中，加载 Utility Network 实质上是：

> 1. 加载一组 FeatureLayer（管道、阀门等）
> 2. 使用 Network 引擎计算规则和拓扑

### Rule Engine 分类:
| 类型                          | 作用                   | 示例描述                                       |
| ----------------------------- | ---------------------- | ---------------------------------------------- |
| Connectivity Rule             | 规定节点与边的连接关系 | “泵站（Junction）只能连接管道（Edge）类型的管线” |
| Subnetwork Controller         | 定义子网络边界         | “阀门关闭时，该支路电力网络被隔离”             |
| Asset Group / Asset Type Rule | 控制设备类型属性或行为 | “水表只能属于住宅管网分组”                     |
| Attribute Rule                | 自动计算字段或更新状态 | “管线压力 > 10 时，状态字段标记为高压”         |

### 内置规则库举例（Esri 提供的）

> 线类要素可用的规则
> - Must Not Have Dangles
> - Must Not Overlap
> - Must Not Self-Overlap
> - Must Not Self-Intersect

>多边形要素可用的规则
> - Must Not Have Gaps
> - Must Not Overlap
> - Must Be Covered By Feature Class Of (其他面类)

>点/线/面混合要素可用的规则
> - Point Must Be Covered By Line Endpoint
> - Line Must Be Inside Polygon
> - Polygon Boundary Must Be Covered By Line

📌 这些都是 ArcGIS 内置规则类型，你可以在 ArcGIS Pro 里选择添加。
它们就像 规则模板库。当你创建一个 Topology 数据集（比如给“河流”这张 FeatureClass 建拓扑），默认是 没有规则的。你需要自己指定：
> - 哪个 FeatureClass（或多个 FeatureClass）参与拓扑
> - 选用哪些规则（从内置规则库里挑）

### 自定义规则

内置规则类型有限，ArcGIS 不允许你“写一个新的规则类型”。但你可以通过 Attribute Rules (Arcade 表达式，在geoDatbase/file 中)  来实现更复杂的自定义检查。
```
// 确保字段 "面积" 大于 0
if ($feature.面积 <= 0) {
    return {
        "errorMessage": "面积必须大于 0"
    };
}
return true;
```


### Runtime SDK 在本地添加 rule:
```csharp
// 创建连通规则
var connectivityRule = new ConnectivityRule(
    junctionAssetType: waterValveJunction,
    edgeAssetType: waterPipeEdge,
    ruleName: "ValveConnectPipe",
    allowsConnectivity: true
);
// 把规则加到当前 UtilityNetwork 对象的内存中，也就是 本地附加规则
await utilityNetwork.AddConnectivityRuleAsync(connectivityRule);
```
ArcGIS Pro / ArcGIS Enterprise 手动添加 Json rule:
```
{
  "name": "HighPressureFlag",
  "type": "calculation",
  "expression": "if ($feature.Pressure > 10) { return 'High'; } else { return 'Normal'; }",
  "trigger": ["Insert", "Update"]
}

```
高级场景: 也可以手动写 JSON 文件，然后通过 SDK 或 ArcGIS 工具导入到 Geodatabase

---

## 4️⃣ 图层与数据操作总结

| 操作对象                | 可操作内容     | API 示例                                                                           | 注意事项                    |
| ------------------- | --------- | -------------------------------------------------------------------------------- | ----------------------- |
| **Layer**           | 显示/符号/过滤  | `setVisible`, `setRenderer`, `setOpacity`, `setDefinitionExpression`             | 不保证修改底层数据               |
| **FeatureTable**    | CRUD / 编辑 | `AddFeatureAsync`, `UpdateFeatureAsync`, `DeleteFeatureAsync`, `ApplyEditsAsync` | 必须可写（服务或本地 Geodatabase） |
| **GraphicsOverlay** | 临时绘制      | `AddGraphic`, `RemoveGraphic`                                                    | 不持久化，可随意操作              |

> ✅ 提示：**SQLite / Mobile Geodatabase** 可以直接通过 API 执行存取操作，不需要手动建表或写脚本，SDK 会自动处理字段、类型、拓扑规则。
---

## 🔹 核心理解

1. **Map/Scene = 画布**
2. **Layer = 视图层**，控制显示效果，不改数据
3. **FeatureTable = 数据层**，真正存储要素和属性，可编辑
4. **GraphicsOverlay = 临时层**，灵活绘制，不持久化
5. **Utility Network = 数据 + 规则 + 拓扑**，通过 FeatureLayer 加载并由引擎管理
