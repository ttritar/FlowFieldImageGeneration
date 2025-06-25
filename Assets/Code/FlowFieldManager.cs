using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class FlowFieldManager : MonoBehaviour
{
    public static FlowFieldManager Instance { get; private set; }

    [Header("Grid Settings")]
    [SerializeField] private Grid _grid;
    [SerializeField] private int _nrCollumns = 10;
    [SerializeField] private int _nrRows = 10;

    private Vector2[,] _flowField;
    private float[,] _widthMap;
    private Color[,] _colorMap;



    [Header("Flow Customization")]
    [SerializeField] private bool _applyNoise = true;
    [SerializeField] private float _noiseScale = 0.1f;


    [Header("Bezier Curve Settings")]
    [SerializeField] private GameObject _bezierLinePrefab;
    [SerializeField] private int _bezierSegments = 10;

    [Header("Visualization Settings")]
    [SerializeField] private Color[] _colorPallet = new Color[1] { Color.black};
    [SerializeField] private float _maxLineWidth;
    [SerializeField] private float _minLineWidth;


    // INITIALIZATION
    //-------------------------------------------------------------

    void Start()
    {
        if (_grid == null)
        {
            Debug.LogError("[FlowFieldManager] Grid not set in inspector.");
            return;
        }

        GenerateFlowMap();


        // DRAW
        DrawBezier();
    }


    void GenerateFlowMap()
    {
        _flowField = new Vector2[_nrCollumns, _nrRows];
        _widthMap = new float[_nrCollumns, _nrRows];
        _colorMap = new Color[_nrCollumns, _nrRows];

        for (int x = 0; x < _nrCollumns; x++)
        {
            for (int y = 0; y < _nrRows; y++)
            {
                if(_applyNoise)
                    _flowField[x, y] = ApplyNoise(x,y);

                _widthMap[x, y] = UnityEngine.Random.value;
                _colorMap[x, y] = _colorPallet[UnityEngine.Random.Range(0, _colorPallet.Length)];

            }
        }
    }

    Vector2 ApplyNoise(int x, int y)
    {
        float noiseX = x * _noiseScale;
        float noiseY = y * _noiseScale;

        float noiseValue = Mathf.PerlinNoise(noiseX, noiseY);
        float angle = noiseValue * Mathf.PI * 2f;

        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
    }


    // UPDATING
    //-------------------------------------------------------------

    //---- Bezier Curve ----
    Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        return uuu * p0 +
               3 * uu * t * p1 +
               3 * u * tt * p2 +
               ttt * p3;
    }

    void CreateBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float widthStart, float widthEnd, Color colorStart, Color colorEnd)
    {
        GameObject lineObj = Instantiate(_bezierLinePrefab, Vector3.zero, Quaternion.identity);
        LineRenderer lr = lineObj.GetComponent<LineRenderer>();
        lr.positionCount = _bezierSegments + 1;

        //--- Width ---
        lr.widthCurve = new AnimationCurve(
            new Keyframe(0, widthStart),
            new Keyframe(1, widthEnd)
        );

        //--- Color ---
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(colorStart, 0.0f),
                new GradientColorKey(colorEnd, 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1, 0.0f),
                new GradientAlphaKey(1, 1.0f)
            }
        );
        lr.colorGradient = gradient;

        //--- Points ---
        for (int i = 0; i <= _bezierSegments; i++)
        {
            float t = i / (float)_bezierSegments;
            Vector3 point = CalculateBezierPoint(t, p0, p1, p2, p3);
            lr.SetPosition(i, point);
        }
    }

    void DrawBezier()
    {
        for (int x = 0; x < _nrCollumns; x++)
        {
            for (int y = 0; y < _nrRows; y++)
            {
                Vector3 p0 = _grid.GetCellCenterWorld(new Vector3Int(y, x, 0));
                Vector2 flowStart = _flowField[x, y];
                float widthStart = Mathf.Lerp(_minLineWidth, _maxLineWidth, _widthMap[x, y]);
                Color colorStart = _colorMap[x, y];

                //--- RIGHT --- (h neighbour)
                if (x + 1 < _nrCollumns)
                {
                    Vector3 p3 = _grid.GetCellCenterWorld(new Vector3Int(y, x + 1, 0));
                    Vector2 flowEnd = _flowField[x + 1, y];
                    float widthEnd = Mathf.Lerp(_minLineWidth, _maxLineWidth, _widthMap[x + 1, y]);
                    Color colorEnd = _colorMap[x + 1, y];
                
                    Vector3 p1 = p0 + (Vector3)(flowStart.normalized * _grid.cellSize.x * 0.5f);
                    Vector3 p2 = p3 - (Vector3)(flowEnd.normalized * _grid.cellSize.x * 0.5f);
                
                    CreateBezier(p0, p1, p2, p3, widthStart, widthEnd, colorStart, colorEnd);
                }

                //--- UP --- (v neighbour)
                if (y + 1 < _nrRows)
                {
                    Vector3 p3 = _grid.GetCellCenterWorld(new Vector3Int(y + 1, x, 0));
                    Vector2 flowEnd = _flowField[x, y + 1];
                    float widthEnd = Mathf.Lerp(_minLineWidth, _maxLineWidth, _widthMap[x, y + 1]);
                    Color colorEnd = _colorMap[x, y + 1];

                    Vector3 p1 = p0 + (Vector3)(flowStart.normalized * _grid.cellSize.y * 0.5f);
                    Vector3 p2 = p3 - (Vector3)(flowEnd.normalized * _grid.cellSize.y * 0.5f);

                    CreateBezier(p0, p1, p2, p3, widthStart, widthEnd, colorStart, colorEnd);
                }
            }
        }
    }


    void OnDrawGizmos()
   {
       Gizmos.color = Color.green;
       for (int x = 0; x < _nrCollumns; x++)
       {
           for (int y = 0; y < _nrRows; y++)
           {
               Vector3 worldPos = _grid.GetCellCenterWorld(new Vector3Int(
                   y,
                   x,
                   0));
   
               Vector2 flow = _flowField[x, y];
   
               Gizmos.DrawLine(worldPos, worldPos + (Vector3)flow * 0.4f);
               Gizmos.DrawSphere(worldPos + (Vector3)flow * 0.4f, 0.1f);
           }
       }
   }
}
