using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MatrixVisualizerCubes : MonoBehaviour
{
    // Путь к файлам
    public string modelFileName = "model.json";
    public string spaceFileName = "space.json";
    public string offsetExportFileName = "offsetExport.json";
    public float tolerance = 0.0001f;

    // Цвета и настройки кубов
    public Color modelCubeColor = Color.blue;
    public Color transformedCubeColor = Color.green;
    public Color spaceCubeColor = Color.white;
    public Vector3 cubeScale = new Vector3(0.5f, 0.5f, 0.5f);

    public int selectedOffsetIndex = 0;

    // Списки созданных кубов
    private List<GameObject> modelCubes = new List<GameObject>();
    private List<GameObject> transformedCubes = new List<GameObject>();
    private List<GameObject> spaceCubes = new List<GameObject>();

    // Экземпляр MatrixMatcher
    private MatrixMatcher matcher;

    private Transform _transform;

    private void Start()
    {
        _transform = transform;
        
        // Формируем полные пути к файлам в StreamingAssets
        string modelPath = Path.Combine(Application.streamingAssetsPath, modelFileName);
        string spacePath = Path.Combine(Application.streamingAssetsPath, spaceFileName);
        string exportPath = Path.Combine(Application.streamingAssetsPath, offsetExportFileName);

        matcher = new MatrixMatcher(modelPath, spacePath, tolerance);
        matcher.LoadMatrices(exportPath);

        // Генерируем кандидатов и валидные смещения (если нужно, можно их использовать для логики)
        List<Vector3> candidates = matcher.GenerateCandidateOffsets();
        List<Vector3> validOffsets = matcher.ValidateOffsets(candidates);

        if (validOffsets.Count > 0)
            Debug.Log("Найдено валидных смещений: " + validOffsets.Count);
        else
            Debug.LogWarning("Валидные смещения не найдены");

        // Создаем кубы для визуализации
        CreateCubes(validOffsets);
    }

    private void CreateCubes(List<Vector3> validOffsets)
    {
        ClearCubes(modelCubes);
        ClearCubes(transformedCubes);
        ClearCubes(spaceCubes);

        // Создаем кубы для матриц модели с вращением
        foreach (var m in matcher.GetModelMatrices())
        {
            Vector3 pos = m.GetTranslation();
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(_transform);
            cube.transform.position = pos;
            cube.transform.localScale = cubeScale;
            cube.transform.rotation = m.GetRotationQuaternion();
            SetColor(cube, modelCubeColor);
            modelCubes.Add(cube);
        }

        // Создаем кубы для матриц модели после применения смещения
        if (validOffsets != null && validOffsets.Count > 0 && selectedOffsetIndex < validOffsets.Count)
        {
            Vector3 offset = validOffsets[selectedOffsetIndex];
            foreach (var m in matcher.GetModelMatrices())
            {
                Vector3 pos = m.GetTranslation() + offset;
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(_transform);
                cube.transform.position = pos;
                cube.transform.localScale = cubeScale;
                cube.transform.rotation = m.GetRotationQuaternion();
                SetColor(cube, transformedCubeColor);
                transformedCubes.Add(cube);
            }
        }

        // Создаем кубы для матриц пространства с вращением
        foreach (var s in matcher.GetSpaceMatrices())
        {
            Vector3 pos = s.GetTranslation();
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(_transform);
            cube.transform.position = pos;
            cube.transform.localScale = cubeScale * 0.7f;
            cube.transform.rotation = s.GetRotationQuaternion();
            SetColor(cube, spaceCubeColor);
            spaceCubes.Add(cube);
        }
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
