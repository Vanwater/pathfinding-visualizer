using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Collections;

public enum NodeState {
    Empty,
    Start,
    End,
    Obstacle,
    Visited,
    Frontier,
    Path
}

public class Node {
    public int x;
    public int y;
    public GameObject cube;
    public Renderer rend;
    public Vector3 originPos;
    public NodeState state = NodeState.Empty;
    public Node parent;
    public float cost = 1f;
    public int dist = 0;
    public Material ownMat;
    public TextMeshPro label;

    public Node(int x, int y, GameObject cube) {
        this.x = x;
        this.y = y;
        this.cube = cube;
        this.rend = cube.GetComponent<Renderer>();
        this.originPos = cube.transform.position;
    }
}
/***
 * 地图的控制器，负责管理网格节点的状态、标签和外观。
 */
public class GridMap : MonoBehaviour {
    public Transform gridParent;
    public float tol = 0.5f;
    public float raiseHeight = 0.25f;
    public Color searchLabelColor = Color.white;
    public Color weightLabelColor = Color.black;

    public Material matEmpty;
    public Material matStart;
    public Material matEnd;
    public Material matObstacle;
    public Material matVisited;
    public Material matFrontier;
    public Material matPath;


    public Node[,] nodes;
    public Node startNode;
    public Node endNode;
    public int rows;
    public int cols;

    void Awake() {
        BuildGrid();
    }

    void BuildGrid() {
        List<Transform> kids = new List<Transform>();
        foreach (Transform t in gridParent) {
            kids.Add(t);
        }

        List<float> xs = new List<float>();
        foreach (Transform t in kids) {
            if (!Contains(xs, t.position.x)) {
                xs.Add(t.position.x);
            }
        }
        xs.Sort();

        List<float> ys = new List<float>();
        foreach (Transform t in kids) {
            if (!Contains(ys, t.position.z)) {
                ys.Add(t.position.z);
            }
        }
        ys.Sort();

        cols = xs.Count;
        rows = ys.Count;
        nodes = new Node[cols, rows];

        Dictionary<float, int> xIndex = new Dictionary<float, int>();
        for (int i = 0; i < cols; i++) {
            xIndex[xs[i]] = i;
        }
        Dictionary<float, int> yIndex = new Dictionary<float, int>();
        for (int j = 0; j < rows; j++) {
            yIndex[ys[j]] = j;
        }

        foreach (Transform t in kids) {
            int cx = xIndex[NearestKey(xs, t.position.x)];
            int cy = yIndex[NearestKey(ys, t.position.z)];
            nodes[cx, cy] = new Node(cx, cy, t.gameObject);
        }

        BuildLabels();
    }

    void BuildLabels() {
        for (int x = 0; x < cols; x++) {
            for (int y = 0; y < rows; y++) {
                Node n = nodes[x, y];
                if (n == null) continue;
                TextMeshPro tmp = n.cube.GetComponentInChildren<TextMeshPro>();
                n.label = tmp;
                if (tmp != null) {
                    tmp.color = searchLabelColor;
                    tmp.text = "";
                }
            }
        }
    }

    float animSeq = 0f;

    public void RaiseAnimated(Node n, bool on) {
        if (n == null) return;
        float target = n.originPos.y + (on ? raiseHeight : 0f);
        float delay = (animSeq * 0.02f) % 0.35f;
        animSeq += 1f;
        StartCoroutine(FloatWave(n, target, 0.25f, delay));
    }

    IEnumerator FloatWave(Node n, float target, float dur, float delay) {
        yield return new WaitForSeconds(delay);
        float from = n.cube.transform.position.y;
        float amp = Mathf.Max(0.05f, Mathf.Abs(target - from));
        float t = 0f;
        while (t < dur) {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float y = Mathf.Lerp(from, target, k);
            y += Mathf.Sin(k * Mathf.PI * 4f) * 0.06f * (1f - k) * amp;
            Vector3 q = n.cube.transform.position;
            q.y = y;
            n.cube.transform.position = q;
            yield return null;
        }
        Vector3 z = n.cube.transform.position;
        z.y = target;
        n.cube.transform.position = z;
    }

    public void SetCost(Node n, float c) {
        if (n == null) return;
        n.cost = Mathf.Clamp(c, -5f, 5f);
        ShowWeight(n);
        ApplyEmptyLook(n);
    }

    public float MoveCost(Node n) {
        return n.cost;
    }

    void ApplyEmptyLook(Node n) {
        if (n.rend == null || matEmpty == null) return;
        if (Mathf.Approximately(n.cost, 1f)) {
            n.rend.sharedMaterial = matEmpty;
            return;
        }
        Color baseColor = matEmpty.color;
        float b = 1f - (n.cost - 1f) * 0.09f;
        b = Mathf.Clamp01(b);
        Color tint = baseColor * b;
        if (n.ownMat == null) {
            n.ownMat = new Material(matEmpty);
        }
        n.ownMat.color = tint;
        n.rend.sharedMaterial = n.ownMat;
    }

    public void SetLabel(Node n, string s) {
        if (n != null && n.label != null) {
            n.label.color = searchLabelColor;
            n.label.text = s;
        }
    }

    public void ShowWeight(Node n) {
        if (n == null || n.label == null) return;
        if (Mathf.Approximately(n.cost, 1f)) {
            n.label.text = "";
            return;
        }
        n.label.color = weightLabelColor;
        n.label.text = FormatCost(n.cost);
    }

    public void HideAllLabels() {
        for (int x = 0; x < cols; x++) {
            for (int y = 0; y < rows; y++) {
                Node n = nodes[x, y];
                if (n != null && n.label != null) n.label.text = "";
            }
        }
    }


    public void ClearDynamicLabels() {
        for (int x = 0; x < cols; x++) {
            for (int y = 0; y < rows; y++) {
                Node n = nodes[x, y];
                if (n != null && n.label != null && Mathf.Approximately(n.cost, 1f)) {
                    n.label.text = "";
                }
            }
        }
    }

    public void RestoreWeightLabels() {
        for (int x = 0; x < cols; x++) {
            for (int y = 0; y < rows; y++) {
                Node n = nodes[x, y];
                if (n != null && n.label != null) {
                    ShowWeight(n);
                }
            }
        }
    }

    string FormatCost(float c) {
        if (Mathf.Approximately(c, Mathf.Floor(c))) {
            return ((int)c).ToString();
        }
        return c.ToString("0.#");
    }

    bool Contains(List<float> list, float v) {
        foreach (float f in list) {
            if (Mathf.Abs(f - v) < tol) {
                return true;
            }
        }
        return false;
    }

    float NearestKey(List<float> list, float v) {
        float best = list[0];
        float bestD = Mathf.Abs(list[0] - v);
        foreach (float f in list) {
            float d = Mathf.Abs(f - v);
            if (d < bestD) {
                bestD = d;
                best = f;
            }
        }
        return best;
    }

    public void SetState(Node n, NodeState s) {
        if (n == null) return;
        n.state = s;
        Material m = MaterialFor(s);
        if (s == NodeState.Empty) {
            ApplyEmptyLook(n);
        } else if (m != null) {
            n.rend.sharedMaterial = m;
        }
    }

    Material MaterialFor(NodeState s) {
        switch (s) {
            case NodeState.Start: return matStart;
            case NodeState.End: return matEnd;
            case NodeState.Obstacle: return matObstacle;
            case NodeState.Visited: return matVisited;
            case NodeState.Frontier: return matFrontier;
            case NodeState.Path: return matPath;
            default: return null;
        }
    }



    public void Raise(Node n, bool on) {
        if (n == null) return;
        Vector3 p = n.originPos;
        if (on) p.y += raiseHeight;
        n.cube.transform.position = p;
    }

    public void ResetAll() {
        startNode = null;
        endNode = null;
        for (int x = 0; x < cols; x++) {
            for (int y = 0; y < rows; y++) {
                Node n = nodes[x, y];
                if (n == null) continue;
                n.cost = 1f;
                SetState(n, NodeState.Empty);
                Raise(n, false);
                n.parent = null;
            }
        }
    }

    public List<Node> GetNeighbors(Node n) {
        List<Node> list = new List<Node>();
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };
        for (int i = 0; i < 4; i++) {
            int nx = n.x + dx[i];
            int ny = n.y + dy[i];
            if (nx >= 0 && nx < cols && ny >= 0 && ny < rows) {
                Node c = nodes[nx, ny];
                if (c != null && c.state != NodeState.Obstacle) {
                    list.Add(c);
                }
            }
        }
        return list;
    }

    public Node NodeFromCube(GameObject cube) {
        for (int x = 0; x < cols; x++) {
            for (int y = 0; y < rows; y++) {
                if (nodes[x, y] != null && nodes[x, y].cube == cube) {
                    return nodes[x, y];
                }
            }
        }
        return null;
    }

    public void ClearVisual() {
        for (int x = 0; x < cols; x++) {
            for (int y = 0; y < rows; y++) {
                Node n = nodes[x, y];
                if (n == null) continue;
                if (n.state == NodeState.Visited || n.state == NodeState.Frontier ||
                    n.state == NodeState.Path) {
                    SetState(n, NodeState.Empty);
                    Raise(n, false);
                }
                n.parent = null;
            }
        }
    }
}
