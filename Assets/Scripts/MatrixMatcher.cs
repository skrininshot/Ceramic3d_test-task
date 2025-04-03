using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class MatrixMatcher
{
    public float tolerance = 0.01f;  // Допуск для сравнения элементов матриц

    private List<MatrixData> modelMatrices = new List<MatrixData>();
    private List<MatrixData> spaceMatrices = new List<MatrixData>();

    private string modelFilePath;
    private string spaceFilePath;

    public MatrixMatcher(string modelFilePath, string spaceFilePath, float tolerance = 0.01f)
    {
        this.modelFilePath = modelFilePath;
        this.spaceFilePath = spaceFilePath;
        this.tolerance = tolerance;
    }

    // Загрузка матриц из JSON-файлов
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

    // Парсинг JSON-массива матриц (формат как в исходном файле)
    private List<MatrixData> ParseMatrixData(string json)
    {
        string wrappedJson = "{\"matrices\":" + json + "}";
        MatrixDataListWrapper wrapper = JsonUtility.FromJson<MatrixDataListWrapper>(wrappedJson);
        return new List<MatrixData>(wrapper.matrices);
    }

    // Публичные методы для получения списков матриц
    public List<MatrixData> GetModelMatrices()
    {
        return modelMatrices;
    }
    public List<MatrixData> GetSpaceMatrices()
    {
        return spaceMatrices;
    }

    // Генерация кандидатных смещений в виде 4x4 матриц, используя опорную матрицу модели (первую)
    public List<Matrix4x4> GenerateCandidateOffsets()
    {
        List<Matrix4x4> candidates = new List<Matrix4x4>();
        if (modelMatrices.Count == 0)
        {
            Debug.LogError("Нет матриц модели.");
            return candidates;
        }

        // Опорная матрица модели
        MatrixData refMatrixData = modelMatrices[0];
        Matrix4x4 M_ref = refMatrixData.ToMatrix4x4();

        foreach (var s in spaceMatrices)
        {
            Matrix4x4 S = s.ToMatrix4x4();
            // Если сравниваем только вращательный блок (верхние 3x3)
            if (Matrix3x3ApproximatelyEquals(M_ref, S, tolerance))
            {
                Matrix4x4 invM = M_ref.inverse;
                Matrix4x4 candidate = S * invM;
                // Добавляем кандидата, если он не похож на уже найденных
                if (!ContainsCandidate(candidates, candidate, tolerance))
                {
                    candidates.Add(candidate);
                }
            }
        }
        Debug.Log("Сгенерировано кандидатных смещений (матриц): " + candidates.Count);
        return candidates;
    }

    // Валидация кандидатных смещений: проверяем, что для каждой матрицы модели T * M ≈ S для хотя бы одной S
    public List<Matrix4x4> ValidateOffsets(List<Matrix4x4> candidates)
    {
        List<Matrix4x4> valid = new List<Matrix4x4>();

        foreach (var T_candidate in candidates)
        {
            bool candidateValid = true;
            foreach (var m in modelMatrices)
            {
                Matrix4x4 M = m.ToMatrix4x4();
                Matrix4x4 transformed = T_candidate * M;
                bool foundMatch = false;
                foreach (var s in spaceMatrices)
                {
                    Matrix4x4 S = s.ToMatrix4x4();
                    if (MatrixApproximatelyEqual(transformed, S, tolerance))
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
                valid.Add(T_candidate);
        }
        Debug.Log("Валидных смещений (матриц) найдено: " + valid.Count);
        return valid;
    }

    // Проверка, содержится ли уже похожая кандидатная матрица в списке (сравнение всех 16 элементов)
    private bool ContainsCandidate(List<Matrix4x4> list, Matrix4x4 candidate, float tol)
    {
        foreach (var mat in list)
        {
            if (MatrixApproximatelyEqual(mat, candidate, tol))
                return true;
        }
        return false;
    }

    // Сравнение двух 4x4 матриц по всем элементам с заданным допуском
    private bool MatrixApproximatelyEqual(Matrix4x4 A, Matrix4x4 B, float tol)
    {
        for (int i = 0; i < 16; i++)
        {
            if (Mathf.Abs(A[i] - B[i]) > tol)
                return false;
        }
        return true;
    }

    // Сравнение только верхних 3x3 блоков двух 4x4 матриц
    private bool Matrix3x3ApproximatelyEquals(Matrix4x4 A, Matrix4x4 B, float tol)
    {
        if (Mathf.Abs(A.m00 - B.m00) > tol) return false;
        if (Mathf.Abs(A.m01 - B.m01) > tol) return false;
        if (Mathf.Abs(A.m02 - B.m02) > tol) return false;
        if (Mathf.Abs(A.m10 - B.m10) > tol) return false;
        if (Mathf.Abs(A.m11 - B.m11) > tol) return false;
        if (Mathf.Abs(A.m12 - B.m12) > tol) return false;
        if (Mathf.Abs(A.m20 - B.m20) > tol) return false;
        if (Mathf.Abs(A.m21 - B.m21) > tol) return false;
        if (Mathf.Abs(A.m22 - B.m22) > tol) return false;
        return true;
    }

    // Экспорт валидных смещений в JSON (каждая смещающая матрица экспортируется целиком)
    public void ExportOffsetsToJson(List<Matrix4x4> offsets, string outputPath)
    {
        if (offsets == null || offsets.Count == 0)
        {
            Debug.LogError("Список смещений пуст. Нечего экспортировать.");
            return;
        }
        
        List<ExportMatrix> exportList = new List<ExportMatrix>();
        foreach (var m in offsets)
        {
            exportList.Add(new ExportMatrix(m));
        }

        string json = JsonConvert.SerializeObject(exportList, Formatting.Indented);
        File.WriteAllText(outputPath, json);
        Debug.Log("Смещения успешно экспортированы в файл: " + outputPath);
    }
}

// Класс для хранения данных одной 4x4 матрицы, как в исходном формате
[Serializable]
public class MatrixData
{
    public float m00, m10, m20, m30;
    public float m01, m11, m21, m31;
    public float m02, m12, m22, m32;
    public float m03, m13, m23, m33;

    public Matrix4x4 ToMatrix4x4()
    {
        Matrix4x4 mat = new Matrix4x4();
        mat.m00 = m00; mat.m01 = m01; mat.m02 = m02; mat.m03 = m03;
        mat.m10 = m10; mat.m11 = m11; mat.m12 = m12; mat.m13 = m13;
        mat.m20 = m20; mat.m21 = m21; mat.m22 = m22; mat.m23 = m23;
        mat.m30 = m30; mat.m31 = m31; mat.m32 = m32; mat.m33 = m33;
        return mat;
    }
}

// Обертка для JSON-парсинга массива матриц
[Serializable]
public class MatrixDataListWrapper
{
    public MatrixData[] matrices;
}

// Класс для экспорта матрицы в JSON
[Serializable]
public class ExportMatrix
{
    public float e00, e01, e02, e03;
    public float e10, e11, e12, e13;
    public float e20, e21, e22, e23;
    public float e30, e31, e32, e33;

    public ExportMatrix(Matrix4x4 mat)
    {
        e00 = mat.m00; e01 = mat.m01; e02 = mat.m02; e03 = mat.m03;
        e10 = mat.m10; e11 = mat.m11; e12 = mat.m12; e13 = mat.m13;
        e20 = mat.m20; e21 = mat.m21; e22 = mat.m22; e23 = mat.m23;
        e30 = mat.m30; e31 = mat.m31; e32 = mat.m32; e33 = mat.m33;
    }
}