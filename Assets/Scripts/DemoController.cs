using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public enum ToolMode {
    None,
    Start,
    End,
    Obstacle,
    WeightUp,
    WeightDown,
    Run
}

public enum SearchAlgo {
    BFS,
    DFS,
    Greedy,
    Dijkstra,
    AStar,
    BiBFS,
    Floyd,
    BellmanFord
}
/***
 * 算法演示控制器的代码
 * 三个模块：1.Buttons控制 2.算法控制 3.逻辑交互以及杂项
 ***/
public class DemoController : MonoBehaviour {
    public GridMap grid;
    public Transform[] toolCubes;
    public Transform[] algoCubes;
    public Transform restartButton;
    public Transform clearButton;
    public float stepInterval = 0.5f;

    public Material buttonActiveMaterial;

    public TMP_Text infoTMP;

    public Color toolActiveColor = Color.magenta;
    public float toolRaise = 0.4f;

    public AudioSource audio;
    public AudioClip soundSearchDone;
    public AudioClip soundPathStep;
    public AudioClip soundClick;
    public AudioClip soundBlock;
    float lastSfx;
    float lastStoneSfx;


    static readonly string[] ALGO_NAMES = {
        "BFS", "DFS", "Greedy", "Dijkstra", "A*", "Bi-BFS", "Floyd", "Bellman-Ford"
    };

    ToolMode mode = ToolMode.None;
    SearchAlgo algo = SearchAlgo.BFS;
    int activeTool = -1;
    int activeAlgo = -1;
    Coroutine searchRoutine;
    Node lastPainted;

    int visitedCount;
    int loopSteps;
    int pathSteps;
    float totalCost;
    float searchTime;
    float searchStart;
    bool reachable;

    Vector3[] toolOrigin;
    Vector3[] algoOrigin;
    Material[] toolMat;
    Material[] algoMat;

    void Start() {
        toolOrigin = new Vector3[toolCubes.Length];
        toolMat = new Material[toolCubes.Length];
        for (int i = 0; i < toolCubes.Length; i++) {
            toolOrigin[i] = toolCubes[i].position;
            Renderer r = toolCubes[i].GetComponent<Renderer>();
            if (r != null) toolMat[i] = r.sharedMaterial;
        }
        algoOrigin = new Vector3[algoCubes.Length];
        algoMat = new Material[algoCubes.Length];
        for (int i = 0; i < algoCubes.Length; i++) {
            algoOrigin[i] = algoCubes[i].position;
            Renderer r = algoCubes[i].GetComponent<Renderer>();
            if (r != null) algoMat[i] = r.sharedMaterial;
        }
        SetAlgo(0);
    }

    void Update() {
        for (int i = 0; i < 8; i++) {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SetAlgo(i);
        }
        if (Input.GetKeyDown(KeyCode.Space)) StartSimulation();

        bool pressed = Input.GetMouseButtonDown(0);
        bool held = Input.GetMouseButton(0);
        if (!pressed && !held) {
            lastPainted = null;
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, 1000f)) {
            lastPainted = null;
            return;
        }

        if (pressed) {
            if (restartButton != null && hit.collider.gameObject == restartButton.gameObject) {
                RestartAll();
                return;
            }
            if (clearButton != null && hit.collider.gameObject == clearButton.gameObject) {
                ClearSearchVisuals();
                return;
            }
            int ti = IndexOf(toolCubes, hit.collider.gameObject);
            if (ti >= 0) {
                OnToolClicked(ti);
                lastPainted = null;
                return;
            }
            int ai = IndexOf(algoCubes, hit.collider.gameObject);
            if (ai >= 0) {
                SetAlgo(ai);
                lastPainted = null;
                return;
            }
        }

        Node n = grid.NodeFromCube(hit.collider.gameObject);
        if (n == null) return;

        if (mode == ToolMode.Obstacle || mode == ToolMode.WeightUp ||
            mode == ToolMode.WeightDown) {
            if (n != lastPainted) {
                lastPainted = n;
                if (mode == ToolMode.Obstacle) {
                    ToggleObstacle(n);
                } else if (n.state != NodeState.Obstacle) {
                    SfxBlock();
                    float nc = (mode == ToolMode.WeightUp) ? n.cost + 1f : n.cost - 1f;
                    grid.SetCost(n, nc);
                }
            }
        } else if (pressed) {
            if (mode == ToolMode.Start) {
                PlaceStart(n);
            } else if (mode == ToolMode.End) {
                PlaceEnd(n);
            }
        }

        if (!held) {
            lastPainted = null;
        }
    }

    int IndexOf(Transform[] arr, GameObject go) {
        for (int i = 0; i < arr.Length; i++) {
            if (arr[i] != null && arr[i].gameObject == go) return i;
        }
        return -1;
    }

    void Sfx(AudioClip c, float v) {
        if (audio == null || c == null) return;
        audio.PlayOneShot(c, v);
    }

    void SfxStone() {
        if (Time.time - lastStoneSfx < 0.08f) return;
        lastStoneSfx = Time.time;
        Sfx(soundSearchDone, 0.4f);
    }

    void SfxBlock() {
        if (Time.time - lastSfx < 0.06f) return;
        lastSfx = Time.time;
        Sfx(soundBlock, 0.6f);
    }

    void OnToolClicked(int i) {
        Sfx(soundClick, 1f);
        if (i == 3) {
            StartSimulation();
            return;
        }
        switch (i) {
            case 0: mode = ToolMode.Start; break;
            case 1: mode = ToolMode.End; break;
            case 2: mode = ToolMode.Obstacle; break;
            case 4: mode = ToolMode.WeightUp; break;
            case 5: mode = ToolMode.WeightDown; break;
            default: return;
        }
        SetToolVisual(i);
    }

    void PlaceStart(Node n) {
        SfxBlock();
        if (grid.startNode != null && grid.startNode != n) {
            grid.SetState(grid.startNode, NodeState.Empty);
            grid.RaiseAnimated(grid.startNode, false);
        }
        grid.startNode = n;
        grid.SetState(n, NodeState.Start);
        grid.RaiseAnimated(n, true);
    }

    void PlaceEnd(Node n) {
        SfxBlock();
        if (grid.endNode != null && grid.endNode != n) {
            grid.SetState(grid.endNode, NodeState.Empty);
            grid.RaiseAnimated(grid.endNode, false);
        }
        grid.endNode = n;
        grid.SetState(n, NodeState.End);
        grid.RaiseAnimated(n, true);
    }

    void ToggleObstacle(Node n) {
        SfxBlock();
        if (n.state == NodeState.Obstacle) {
            grid.SetState(n, NodeState.Empty);
            grid.RaiseAnimated(n, false);
        } else if (n != grid.startNode && n != grid.endNode) {
            grid.SetState(n, NodeState.Obstacle);
            grid.RaiseAnimated(n, true);
        }
    }

    void SetToolVisual(int idx) {
        if (activeTool >= 0 && activeTool < toolCubes.Length) {
            toolCubes[activeTool].position = toolOrigin[activeTool];
            ApplyButtonMat(toolCubes[activeTool], toolMat[activeTool]);
        }
        activeTool = idx;
        if (activeTool >= 0 && activeTool < toolCubes.Length) {
            toolCubes[activeTool].position = toolOrigin[activeTool] + Vector3.up * toolRaise;
            if (buttonActiveMaterial == null) {
                SetColor(toolCubes[activeTool], toolActiveColor);
            } else {
                ApplyButtonMat(toolCubes[activeTool], buttonActiveMaterial);
            }
        }
    }

    void ResetToolVisual() {
        if (activeTool >= 0 && activeTool < toolCubes.Length) {
            toolCubes[activeTool].position = toolOrigin[activeTool];
            ApplyButtonMat(toolCubes[activeTool], toolMat[activeTool]);
        }
        activeTool = -1;
    }

    void SetAlgo(int i) {
        Sfx(soundClick, 1f);
        if (i < 0 || i >= 8) return;
        algo = (SearchAlgo)i;
        if (activeAlgo >= 0 && activeAlgo < algoCubes.Length) {
            algoCubes[activeAlgo].position = algoOrigin[activeAlgo];
            ApplyButtonMat(algoCubes[activeAlgo], algoMat[activeAlgo]);
        }
        activeAlgo = i;
        if (activeAlgo >= 0 && activeAlgo < algoCubes.Length) {
            algoCubes[activeAlgo].position = algoOrigin[activeAlgo] + Vector3.up * toolRaise;
            if (buttonActiveMaterial == null) {
                SetColor(algoCubes[activeAlgo], toolActiveColor);
            } else {
                ApplyButtonMat(algoCubes[activeAlgo], buttonActiveMaterial);
            }
        }
    }

    void ApplyButtonMat(Transform t, Material m) {
        if (t == null || m == null) return;
        Renderer r = t.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = m;
    }

    void SetColor(Transform t, Color c) {
        Renderer r = t.GetComponent<Renderer>();
        if (r != null) r.material.color = c;
    }

    void RestartAll() {
        if (searchRoutine != null) {
            StopCoroutine(searchRoutine);
            searchRoutine = null;
        }
        lastPainted = null;
        mode = ToolMode.None;
        grid.ResetAll();
        grid.HideAllLabels();
        ResetToolVisual();
        ShowInfo("");
    }

    void ClearSearchVisuals() {
        Sfx(soundClick, 1f);
        if (searchRoutine != null) {
            StopCoroutine(searchRoutine);
            searchRoutine = null;
        }
        lastPainted = null;
        grid.ClearVisual();
        grid.ClearDynamicLabels();
        grid.RestoreWeightLabels();
        ShowInfo("");
    }

    void ShowInfo(string s) {
        if (infoTMP != null) infoTMP.text = s;
    }

    void ShowResult() {
        string algoName = ALGO_NAMES[(int)algo];
        searchTime = Time.realtimeSinceStartup - searchStart;
        string msg;
        if (reachable) {
            msg = algoName + " reachable\nPath: " + pathSteps + " cells" +
                  "\nCost: " + totalCost.ToString("0.##") +
                  "\nVisited: " + visitedCount +
                  "\nSteps: " + loopSteps +
                  "\nTime: " + searchTime.ToString("0.0") + "s";
        } else {
            msg = algoName + " unreachable\nVisited: " + visitedCount +
                  "\nTime: " + searchTime.ToString("0.0") + "s";
        }
        ShowInfo(msg);
    }

    void StartSimulation() {
        if (grid.startNode == null || grid.endNode == null) return;
        if (searchRoutine != null) {
            StopCoroutine(searchRoutine);
            searchRoutine = null;
        }
        grid.ClearVisual();
        grid.ClearDynamicLabels();
        visitedCount = 0;
        loopSteps = 0;
        pathSteps = 0;
        totalCost = 0f;
        searchTime = 0f;
        reachable = false;
        searchStart = Time.realtimeSinceStartup;
        searchRoutine = StartCoroutine(SearchRoutine());
    }

    IEnumerator SearchRoutine() {
        switch (algo) {
            case SearchAlgo.BFS: yield return BFS(); break;
            case SearchAlgo.DFS: yield return DFS(); break;
            case SearchAlgo.Greedy: yield return BestFirst(true); break;
            case SearchAlgo.Dijkstra: yield return BestFirst(false); break;
            case SearchAlgo.AStar: yield return AStar(); break;
            case SearchAlgo.BiBFS: yield return BiBFS(); break;
            case SearchAlgo.Floyd: yield return Floyd(); break;
            case SearchAlgo.BellmanFord: yield return BellmanFord(); break;
        }
        searchRoutine = null;
    }

    float H(Node a, Node b) {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    void PaintVisited(Node n) {
        SfxStone();
        if (n.state != NodeState.Start && n.state != NodeState.End) {
            grid.SetState(n, NodeState.Visited);
            grid.RaiseAnimated(n, true);
        }
    }

    void PaintFrontier(Node n) {
        if (n.state != NodeState.Start && n.state != NodeState.End) {
            grid.SetState(n, NodeState.Frontier);
            grid.RaiseAnimated(n, true);
        }
    }

    IEnumerator BFS() {
        Queue<Node> q = new Queue<Node>();
        q.Enqueue(grid.startNode);
        grid.startNode.parent = null;
        while (q.Count > 0) {
            Node cur = q.Dequeue();
            PaintVisited(cur);
            visitedCount++;
            if (cur == grid.endNode) {
                reachable = true;
                yield return RenderPath(BuildPath(cur));
                yield break;
            }
            foreach (Node nb in grid.GetNeighbors(cur)) {
                if (nb.parent == null && nb != grid.startNode) {
                    nb.parent = cur;
                    nb.dist = cur.dist + 1;
                    grid.SetLabel(nb, nb.dist.ToString());
                    PaintFrontier(nb);
                    q.Enqueue(nb);
                }
            }
            loopSteps++;
            yield return new WaitForSeconds(stepInterval);
        }
        grid.RestoreWeightLabels();
        ShowResult();
    }

    IEnumerator DFS() {
        Stack<Node> st = new Stack<Node>();
        st.Push(grid.startNode);
        grid.startNode.parent = null;
        while (st.Count > 0) {
            Node cur = st.Pop();
            PaintVisited(cur);
            visitedCount++;
            if (cur == grid.endNode) {
                reachable = true;
                yield return RenderPath(BuildPath(cur));
                yield break;
            }
            List<Node> nbs = grid.GetNeighbors(cur);
            nbs.Reverse();
            foreach (Node nb in nbs) {
                if (nb.parent == null && nb != grid.startNode) {
                    nb.parent = cur;
                    nb.dist = cur.dist + 1;
                    grid.SetLabel(nb, nb.dist.ToString());
                    PaintFrontier(nb);
                    st.Push(nb);
                }
            }
            loopSteps++;
            yield return new WaitForSeconds(stepInterval);
        }
        grid.RestoreWeightLabels();
        ShowResult();
    }

    IEnumerator BestFirst(bool greedy) {
        List<Node> open = new List<Node>();
        Dictionary<Node, float> g = new Dictionary<Node, float>();
        HashSet<Node> closed = new HashSet<Node>();
        open.Add(grid.startNode);
        g[grid.startNode] = 0;
        grid.startNode.parent = null;

        while (open.Count > 0) {
            float bestF = float.MaxValue;
            Node cur = open[0];
            foreach (Node nd in open) {
                float f = g[nd] + (greedy ? 0f : 0f) + (greedy ? H(nd, grid.endNode) : g[nd]);
                if (f < bestF) {
                    bestF = f;
                    cur = nd;
                }
            }
            open.Remove(cur);
            closed.Add(cur);
            PaintVisited(cur);
            visitedCount++;
            if (cur == grid.endNode) {
                reachable = true;
                yield return RenderPath(BuildPath(cur));
                yield break;
            }
            foreach (Node nb in grid.GetNeighbors(cur)) {
                if (closed.Contains(nb)) continue;
                float ng = g[cur] + grid.MoveCost(nb);
                if (!g.ContainsKey(nb) || ng < g[nb]) {
                    g[nb] = ng;
                    nb.parent = cur;
                    grid.SetLabel(nb, ((int)ng).ToString());
                    if (!open.Contains(nb)) {
                        open.Add(nb);
                        PaintFrontier(nb);
                    }
                }
            }
            loopSteps++;
            yield return new WaitForSeconds(stepInterval);
        }
        grid.RestoreWeightLabels();
        ShowResult();
    }

    IEnumerator AStar() {
        List<Node> open = new List<Node>();
        Dictionary<Node, float> g = new Dictionary<Node, float>();
        HashSet<Node> closed = new HashSet<Node>();
        open.Add(grid.startNode);
        g[grid.startNode] = 0;
        grid.startNode.parent = null;

        while (open.Count > 0) {
            float bestF = float.MaxValue;
            Node cur = open[0];
            foreach (Node nd in open) {
                float f = g[nd] + H(nd, grid.endNode);
                if (f < bestF) {
                    bestF = f;
                    cur = nd;
                }
            }
            open.Remove(cur);
            closed.Add(cur);
            PaintVisited(cur);
            visitedCount++;
            if (cur == grid.endNode) {
                reachable = true;
                yield return RenderPath(BuildPath(cur));
                yield break;
            }
            foreach (Node nb in grid.GetNeighbors(cur)) {
                if (closed.Contains(nb)) continue;
                float ng = g[cur] + grid.MoveCost(nb);
                if (!g.ContainsKey(nb) || ng < g[nb]) {
                    g[nb] = ng;
                    nb.parent = cur;
                    grid.SetLabel(nb, ((int)ng).ToString());
                    if (!open.Contains(nb)) {
                        open.Add(nb);
                        PaintFrontier(nb);
                    }
                }
            }
            loopSteps++;
            yield return new WaitForSeconds(stepInterval);
        }
        grid.RestoreWeightLabels();
        ShowResult();
    }

    IEnumerator BiBFS() {
        Queue<Node> qf = new Queue<Node>();
        Queue<Node> qb = new Queue<Node>();
        Dictionary<Node, int> distF = new Dictionary<Node, int>();
        Dictionary<Node, int> distB = new Dictionary<Node, int>();
        Dictionary<Node, Node> parentF = new Dictionary<Node, Node>();
        Dictionary<Node, Node> parentB = new Dictionary<Node, Node>();
        HashSet<Node> seenF = new HashSet<Node>();
        HashSet<Node> seenB = new HashSet<Node>();

        qf.Enqueue(grid.startNode);
        qb.Enqueue(grid.endNode);
        seenF.Add(grid.startNode);
        seenB.Add(grid.endNode);
        distF[grid.startNode] = 0;
        distB[grid.endNode] = 0;

        Node meet = null;
        while (qf.Count > 0 && qb.Count > 0 && meet == null) {
            List<Node> qfl = new List<Node>(qf);
            qf.Clear();
            foreach (Node cur in qfl) {
                PaintVisited(cur);
                visitedCount++;
                foreach (Node nb in grid.GetNeighbors(cur)) {
                    if (seenF.Contains(nb)) continue;
                    seenF.Add(nb);
                    parentF[nb] = cur;
                    distF[nb] = distF[cur] + 1;
                    grid.SetLabel(nb, distF[nb].ToString());
                    PaintFrontier(nb);
                    qf.Enqueue(nb);
                    if (seenB.Contains(nb)) {
                        meet = nb;
                        break;
                    }
                }
                if (meet != null) break;
            }
            if (meet != null) break;
            loopSteps++;
            yield return new WaitForSeconds(stepInterval);

            List<Node> qbl = new List<Node>(qb);
            qb.Clear();
            foreach (Node cur in qbl) {
                PaintVisited(cur);
                visitedCount++;
                foreach (Node nb in grid.GetNeighbors(cur)) {
                    if (seenB.Contains(nb)) continue;
                    seenB.Add(nb);
                    parentB[nb] = cur;
                    distB[nb] = distB[cur] + 1;
                    grid.SetLabel(nb, (-distB[nb]).ToString());
                    PaintFrontier(nb);
                    qb.Enqueue(nb);
                    if (seenF.Contains(nb)) {
                        meet = nb;
                        break;
                    }
                }
                if (meet != null) break;
            }
            if (meet != null) break;
            loopSteps++;
            yield return new WaitForSeconds(stepInterval);
        }

        if (meet != null) {
            reachable = true;
            List<Node> path = new List<Node>();
            Node c = meet;
            while (c != null) {
                path.Add(c);
                if (parentF.ContainsKey(c)) c = parentF[c];
                else c = (c == grid.startNode) ? null : null;
            }
            path.Reverse();
            c = parentB.ContainsKey(meet) ? parentB[meet] : null;
            while (c != null) {
                path.Add(c);
                if (parentB.ContainsKey(c)) c = parentB[c];
                else c = null;
            }
            yield return RenderPath(path);
            yield break;
        }
        grid.RestoreWeightLabels();
        ShowResult();
    }

    IEnumerator Floyd() {
        List<Node> valid = new List<Node>();
        for (int x = 0; x < grid.cols; x++) {
            for (int y = 0; y < grid.rows; y++) {
                Node nd = grid.nodes[x, y];
                if (nd != null && nd.state != NodeState.Obstacle) valid.Add(nd);
            }
        }
        int n = valid.Count;
        Dictionary<Node, int> idMap = new Dictionary<Node, int>();
        for (int i = 0; i < n; i++) idMap[valid[i]] = i;

        float INF = 1e9f;
        float[,] dist = new float[n, n];
        int[,] nxt = new int[n, n];
        for (int i = 0; i < n; i++) {
            for (int j = 0; j < n; j++) {
                dist[i, j] = (i == j) ? 0f : INF;
                nxt[i, j] = -1;
            }
        }
        for (int i = 0; i < n; i++) {
            foreach (Node nb in grid.GetNeighbors(valid[i])) {
                int j = idMap[nb];
                dist[i, j] = 1f;
                nxt[i, j] = j;
            }
        }

        int relax = 0;
        for (int k = 0; k < n; k++) {
            for (int i = 0; i < n; i++) {
                if (dist[i, k] >= INF) continue;
                for (int j = 0; j < n; j++) {
                    float nd2 = dist[i, k] + dist[k, j];
                    if (nd2 < dist[i, j]) {
                        dist[i, j] = nd2;
                        nxt[i, j] = nxt[i, k];
                        relax++;
                    }
                }
            }
            loopSteps++;
            visitedCount = relax;
            yield return new WaitForSeconds(0.02f);
        }

        int s = idMap[grid.startNode];
        int t = idMap[grid.endNode];
        if (dist[s, t] >= INF) {
            ShowResult();
            yield break;
        }
        reachable = true;
        List<Node> path = new List<Node>();
        int cur = s;
        while (cur != -1 && cur != t) {
            path.Add(valid[cur]);
            cur = nxt[cur, t];
        }
        path.Add(valid[t]);
        yield return RenderPath(path);
    }


    List<Node> BuildPath(Node end) {
        List<Node> path = new List<Node>();
        Node cur = end;
        while (cur != null) {
            path.Add(cur);
            cur = cur.parent;
        }
        path.Reverse();
        return path;
    }

    struct BFEdge {
        public Node from;
        public Node to;
        public float w;
        public BFEdge(Node a, Node b, float ww) { from = a; to = b; w = ww; }
    }

    IEnumerator BellmanFord() {
        List<Node> valid = new List<Node>();
        for (int x = 0; x < grid.cols; x++) {
            for (int y = 0; y < grid.rows; y++) {
                Node nd = grid.nodes[x, y];
                if (nd != null && nd.state != NodeState.Obstacle) valid.Add(nd);
            }
        }
        List<BFEdge> edges = new List<BFEdge>();
        foreach (Node a in valid) {
            foreach (Node nb in grid.GetNeighbors(a)) {
                edges.Add(new BFEdge(a, nb, nb.cost));
            }
        }

        Dictionary<Node, float> dist = new Dictionary<Node, float>();
        Dictionary<Node, Node> pre = new Dictionary<Node, Node>();
        foreach (Node nd in valid) dist[nd] = 1e9f;
        dist[grid.startNode] = 0f;
        grid.SetLabel(grid.startNode, "0");

        for (int it = 1; it < valid.Count; it++) {
            bool changed = false;
            foreach (BFEdge e in edges) {
                if (dist[e.from] >= 1e8f) continue;
                float nd2 = dist[e.from] + e.w;
                if (nd2 < dist[e.to] - 1e-6f) {
                    dist[e.to] = nd2;
                    pre[e.to] = e.from;
                    grid.SetLabel(e.to, ((int)nd2).ToString());
                    changed = true;
                    visitedCount++;
                }
            }
            loopSteps = it;
            if (!changed) break;
            yield return new WaitForSeconds(stepInterval);
        }

        bool negCycle = false;
        foreach (BFEdge e in edges) {
            if (dist[e.from] >= 1e8f) continue;
            if (dist[e.from] + e.w < dist[e.to] - 1e-6f) {
                negCycle = true;
                break;
            }
        }
        if (negCycle) {
            grid.RestoreWeightLabels();
            ShowInfo("NEGATIVE CYCLE detected");
            yield break;
        }
        if (dist[grid.endNode] >= 1e8f) {
            grid.RestoreWeightLabels();
            ShowResult();
            yield break;
        }
        reachable = true;
        List<Node> path = new List<Node>();
        Node cur = grid.endNode;
        while (cur != null) {
            path.Add(cur);
            if (pre.ContainsKey(cur)) cur = pre[cur];
            else break;
        }
        path.Reverse();
        yield return RenderPath(path);
    }

    IEnumerator RenderPath(List<Node> path) {
        for (int x = 0; x < grid.cols; x++) {
            for (int y = 0; y < grid.rows; y++) {
                Node nd = grid.nodes[x, y];
                if (nd != null && nd.label != null && !path.Contains(nd)) {
                    grid.ShowWeight(nd);
                }
            }
        }
        int step = 0;
        totalCost = 0f;
        foreach (Node p in path) {
            if (p == grid.startNode) continue;
            step++;
            totalCost += grid.MoveCost(p);
            if (p != grid.endNode) {
                grid.SetState(p, NodeState.Path);
            }
            grid.SetLabel(p, step.ToString());
            Sfx(soundPathStep, 0.15f);
            yield return new WaitForSeconds(0.05f);
        }
        pathSteps = step;
        ShowResult();
    }
}