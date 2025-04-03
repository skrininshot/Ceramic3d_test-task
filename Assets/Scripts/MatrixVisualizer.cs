using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;

public class MatrixVisualizer : MonoBehaviour
{
    // Параметры допусков (элементы матрицы)
    public float tolerance = 0.01f;
    public int selectedOffsetIndex = 0;

    // Цвета и настройки кубов
    public Color modelCubeColor = Color.blue;
    public Color transformedCubeColor = Color.green;
    public Color spaceCubeColor = Color.white;
    public Vector3 cubeScale = new Vector3(0.5f, 0.5f, 0.5f);

    // Имена файлов (StreamingAssets)
    public string modelFileName = "model.json";
    public string spaceFileName = "space.json";
    public string offsetExportFileName = "offsetExport.json";

    // Списки созданных кубов
    private List<GameObject> modelCubes = new List<GameObject>();
    private List<GameObject> transformedCubes = new List<GameObject>();
    private List<GameObject> spaceCubes = new List<GameObject>();

    // Наш новый экземпляр класса, работающего с полными матрицами
    private MatrixMatcher matrixMatcher;
    private Transform _transform;

    void Start()
    {
        _transform = transform;

        // Формирование путей
        string modelPath = Path.Combine(Application.streamingAssetsPath, modelFileName);
        string spacePath = Path.Combine(Application.streamingAssetsPath, spaceFileName);
        string exportPath = Path.Combine(Application.streamingAssetsPath, offsetExportFileName);

        // Создаем экземпляр нового класса (с elementTolerance)
        matrixMatcher = new MatrixMatcher(modelPath, spacePath, tolerance);
        matrixMatcher.LoadMatrices();

        // Генерируем кандидатные смещения (полные 4x4 матрицы)
        List<Matrix4x4> candidates = matrixMatcher.GenerateCandidateOffsets();
        List<Matrix4x4> validOffsets = matrixMatcher.ValidateOffsets(candidates);

        if (validOffsets.Count > 0)
        {
            Debug.Log("Найдено валидных смещений (матриц): " + validOffsets.Count);
            // Экспорт валидных смещений, если требуется
            matrixMatcher.ExportOffsetsToJson(validOffsets, exportPath);
        }
        else
        {
            Debug.LogWarning("Валидные смещения не найдены");
        }

        // Визуализируем исходные матрицы модели
        CreateModelCubes();
        // Визуализируем трансформированные матрицы модели, если найден хотя бы один кандидат
        if (validOffsets.Count > 0 && selectedOffsetIndex < validOffsets.Count)
        {
            Matrix4x4 offsetMatrix = validOffsets[selectedOffsetIndex];
            CreateTransformedCubes(offsetMatrix);
        }
        // Визуализируем матрицы пространства
        CreateSpaceCubes();
    }

    // Создаем кубы для матриц модели (исходные)
    private void CreateModelCubes()
    {
        ClearCubes(modelCubes);
        foreach (var mData in matrixMatcher.GetModelMatrices())
        {
            Matrix4x4 M = mData.ToMatrix4x4();
            Vector3 pos = M.GetColumn(3);
            Quaternion rot = GetRotationFromMatrix(M);
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(_transform);
            cube.transform.position = pos;
            cube.transform.rotation = rot;
            cube.transform.localScale = cubeScale;
            SetColor(cube, modelCubeColor);
            modelCubes.Add(cube);
        }
    }

    // Создаем кубы для трансформированных матриц модели (применив смещение)
    private void CreateTransformedCubes(Matrix4x4 offsetMatrix)
    {
        ClearCubes(transformedCubes);
        foreach (var mData in matrixMatcher.GetModelMatrices())
        {
            Matrix4x4 M = mData.ToMatrix4x4();
            // Вычисляем новое преобразование: T_candidate * M
            Matrix4x4 transformed = offsetMatrix * M;
            Vector3 pos = transformed.GetColumn(3);
            Quaternion rot = GetRotationFromMatrix(transformed);
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(_transform);
            cube.transform.position = pos;
            cube.transform.rotation = rot;
            cube.transform.localScale = cubeScale;
            SetColor(cube, transformedCubeColor);
            transformedCubes.Add(cube);
        }
    }

    // Создаем кубы для матриц пространства
    private void CreateSpaceCubes()
    {
        ClearCubes(spaceCubes);
        foreach (var sData in matrixMatcher.GetSpaceMatrices())
        {
            Matrix4x4 S = sData.ToMatrix4x4();
            Vector3 pos = S.GetColumn(3);
            Quaternion rot = GetRotationFromMatrix(S);
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(_transform);
            cube.transform.position = pos;
            cube.transform.rotation = rot;
            cube.transform.localScale = cubeScale * 0.7f;
            SetColor(cube, spaceCubeColor);
            spaceCubes.Add(cube);
        }
    }

    // Простой способ получить Quaternion из матрицы (используем столбцы 2 и 1)
    private Quaternion GetRotationFromMatrix(Matrix4x4 m)
    {
        // Предполагается, что матрица корректно представляет преобразование
        return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
    }

    private void SetColor(GameObject cube, Color color)
    {
        Renderer rend = cube.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Standard"));
            rend.material.color = color;
        }
    }

    private void ClearCubes(List<GameObject> cubes)
    {
        foreach (var go in cubes)
        {
            if (go != null)
                Destroy(go);
        }
        cubes.Clear();
    }
}
