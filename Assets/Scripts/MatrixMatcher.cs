using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MatrixMatcher
{
    public float tolerance = 0.0001f;

    private List<MatrixData> modelMatrices = new List<MatrixData>();
    private List<MatrixData> spaceMatrices = new List<MatrixData>();

    private string modelFilePath;
    private string spaceFilePath;

    public MatrixMatcher(string modelFilePath, string spaceFilePath, float tolerance = 0.0001f)
    {
        this.modelFilePath = modelFilePath;
        this.spaceFilePath = spaceFilePath;
        this.tolerance = tolerance;
    }

    // Метод загрузки матриц из файлов
    public void LoadMatrices()
    {
        if (File.Exists(modelFilePath))
        {
            string jsonText = File.ReadAllText(modelFilePath);
            modelMatrices = ParseMatrixData(jsonText);
            Debug.Log("Считано " + modelMatrices.Count + " матриц модели.");
        }
        else
        {
            Debug.LogError("Файл модели не найден: " + modelFilePath);
        }

        if (File.Exists(spaceFilePath))
        {
            string jsonText = File.ReadAllText(spaceFilePath);
            spaceMatrices = ParseMatrixData(jsonText);
            Debug.Log("Считано " + spaceMatrices.Count + " матриц пространства.");
        }
        else
        {
            Debug.LogError("Файл пространства не найден: " + spaceFilePath);
        }
    }

    // Парсинг JSON-массива матриц
    private List<MatrixData> ParseMatrixData(string json)
    {
        string wrappedJson = "{\"matrices\":" + json + "}";
        MatrixDataListWrapper wrapper = JsonUtility.FromJson<MatrixDataListWrapper>(wrappedJson);
        return new List<MatrixData>(wrapper.matrices);
    }

    // Генерация кандидатов смещений для матриц с совпадающими R-блоками
    public List<Vector3> GenerateCandidateOffsets()
    {
        List<Vector3> candidates = new List<Vector3>();

        foreach (var m in modelMatrices)
        {
            Matrix3x3 R_model = m.GetRotationBlock();
            Vector3 t_model = m.GetTranslation();

            foreach (var s in spaceMatrices)
            {
                Matrix3x3 R_space = s.GetRotationBlock();
                if (R_model.ApproximatelyEquals(R_space, tolerance))
                {
                    Vector3 t_space = s.GetTranslation();
                    Vector3 candidate = t_space - t_model;
                    if (!ContainsCandidate(candidates, candidate, tolerance))
                    {
                        candidates.Add(candidate);
                    }
                }
            }
        }
        Debug.Log("Сгенерировано кандидатов: " + candidates.Count);
        return candidates;
    }

    // Проверка кандидатов смещений: для каждой матрицы модели должно находиться соответствие в пространстве.
    public List<Vector3> ValidateOffsets(List<Vector3> candidates)
    {
        List<Vector3> valid = new List<Vector3>();
        foreach (var candidate in candidates)
        {
            bool candidateValid = true;
            foreach (var m in modelMatrices)
            {
                Matrix3x3 R_model = m.GetRotationBlock();
                Vector3 expectedT = m.GetTranslation() + candidate;
                bool foundMatch = false;
                foreach (var s in spaceMatrices)
                {
                    if (R_model.ApproximatelyEquals(s.GetRotationBlock(), tolerance) &&
                        ApproximatelyEqual(expectedT, s.GetTranslation(), tolerance))
                    {
                        foundMatch = true;
                        break;
                    }
                }
                if (!foundMatch)
                {
                    candidateValid = false;
                    break;
                }
            }
            if (candidateValid)
                valid.Add(candidate);
        }
        return valid;
    }

    // Геттеры
    public List<MatrixData> GetModelMatrices() { return modelMatrices; }
    public List<MatrixData> GetSpaceMatrices() { return spaceMatrices; }

    // Вспомогательная функция сравнения векторов с допуском
    private bool ApproximatelyEqual(Vector3 a, Vector3 b, float tol)
    {
        return Mathf.Abs(a.x - b.x) <= tol &&
               Mathf.Abs(a.y - b.y) <= tol &&
               Mathf.Abs(a.z - b.z) <= tol;
    }

    private bool ContainsCandidate(List<Vector3> list, Vector3 candidate, float tol)
    {
        foreach (var c in list)
        {
            if (ApproximatelyEqual(c, candidate, tol))
                return true;
        }
        return false;
    }
}

// Обертка для JSON-парсинга
[Serializable]
public class MatrixDataListWrapper
{
    public MatrixData[] matrices;
}

// Класс для хранения данных одной 4x4 матрицы
[Serializable]
public class MatrixData
{
    public float m00, m10, m20, m30;
    public float m01, m11, m21, m31;
    public float m02, m12, m22, m32;
    public float m03, m13, m23, m33;

    public Matrix3x3 GetRotationBlock()
    {
        return new Matrix3x3(m00, m01, m02,
            m10, m11, m12,
            m20, m21, m22);
    }

    public Vector3 GetTranslation()
    {
        return new Vector3(m03, m13, m23);
    }

    public Quaternion GetRotationQuaternion()
    {
        Matrix3x3 R = GetRotationBlock();
        float trace = R.a00 + R.a11 + R.a22;
        float w, x, y, z;
        if (trace > 0)
        {
            float s = Mathf.Sqrt(trace + 1.0f) * 2f; 
            w = 0.25f * s;
            x = (R.a21 - R.a12) / s;
            y = (R.a02 - R.a20) / s;
            z = (R.a10 - R.a01) / s;
        }
        else if ((R.a00 > R.a11) && (R.a00 > R.a22))
        {
            float s = Mathf.Sqrt(1.0f + R.a00 - R.a11 - R.a22) * 2f;
            w = (R.a21 - R.a12) / s;
            x = 0.25f * s;
            y = (R.a01 + R.a10) / s;
            z = (R.a02 + R.a20) / s;
        }
        else if (R.a11 > R.a22)
        {
            float s = Mathf.Sqrt(1.0f + R.a11 - R.a00 - R.a22) * 2f;
            w = (R.a02 - R.a20) / s;
            x = (R.a01 + R.a10) / s;
            y = 0.25f * s;
            z = (R.a12 + R.a21) / s;
        }
        else
        {
            float s = Mathf.Sqrt(1.0f + R.a22 - R.a00 - R.a11) * 2f;
            w = (R.a10 - R.a01) / s;
            x = (R.a02 + R.a20) / s;
            y = (R.a12 + R.a21) / s;
            z = 0.25f * s;
        }
        return new Quaternion(x, y, z, w);
    }
}

// Структура для представления 3x3 матрицы
public struct Matrix3x3
{
    public float a00, a01, a02;
    public float a10, a11, a12;
    public float a20, a21, a22;

    public Matrix3x3(float a00, float a01, float a02,
                     float a10, float a11, float a12,
                     float a20, float a21, float a22)
    {
        this.a00 = a00; this.a01 = a01; this.a02 = a02;
        this.a10 = a10; this.a11 = a11; this.a12 = a12;
        this.a20 = a20; this.a21 = a21; this.a22 = a22;
    }

    public bool ApproximatelyEquals(Matrix3x3 other, float tol)
    {
        return Mathf.Abs(a00 - other.a00) <= tol &&
               Mathf.Abs(a01 - other.a01) <= tol &&
               Mathf.Abs(a02 - other.a02) <= tol &&
               Mathf.Abs(a10 - other.a10) <= tol &&
               Mathf.Abs(a11 - other.a11) <= tol &&
               Mathf.Abs(a12 - other.a12) <= tol &&
               Mathf.Abs(a20 - other.a20) <= tol &&
               Mathf.Abs(a21 - other.a21) <= tol &&
               Mathf.Abs(a22 - other.a22) <= tol;
    }
}
