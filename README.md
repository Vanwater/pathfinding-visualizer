# 寻路算法可视化（Unity）

Unity URP 实现的 16×16 网格寻路演示，支持 8 种图搜索算法。

## 功能

- 算法：BFS、DFS、Greedy、Dijkstra、A*、双向 BFS、Floyd、Bellman
- 网格编辑：放置起点/终点/障碍，长按画墙，二次扫过擦除
- 地形权值：每格可调 -5 ~ +5，数字显示权值，颜色随权值变深
- 搜索过程逐步动画，方块波浪式起伏，动态显示当前距离
- 支持负权图
- 音效
- Info 面板：路径长度、总代价、访问节点数、步数、搜索耗时
- Clear 清除搜索痕迹，Restart 完全重置

## 操作

- 右侧工具按钮：起点、终点、障碍、加权、减权、运行
- 左侧算法按钮或数字键 1~8 切换算法
- 空格键运行搜索

## 版本

Unity 2022+ 

## 目录

- `Assets/Scripts/GridMap.cs`：网格构建、节点状态、权值颜色、起伏动画
- `Assets/Scripts/DemoController.cs`：交互、输入、八个算法、信息面板、音效
- `Assets/Audio`：MC 音效资源
- `Assets/Materials`：各状态材质
