namespace FileIO
{
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using UnityEngine;
using UnityEngine.Networking;

public static class TSVReader
{
    private static readonly CsvConfiguration TsvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        Delimiter = "\t",
        Mode = CsvMode.NoEscape,
        HasHeaderRecord = true,
        MissingFieldFound = null,
        HeaderValidated = null,
        // 프로퍼티뿐 아니라 public 필드도 헤더에 매핑한다. (가산적 설정이라 기존 프로퍼티 기반 DTO도 그대로 동작)
        MemberTypes = MemberTypes.Properties | MemberTypes.Fields,
    };

    /// <summary>
    /// Application.persistentDataPath/Table 폴더에서 주어진 테이블 이름의 TSV 파일을 읽어 List<T>로 반환합니다.
    /// </summary>
    /// <typeparam name="T"> 매핑할 클래스 타입 (public getter/setter 필수)</typeparam>
    /// <param name="tableName"> 파일 이름 (확장자 제외)</param>
    /// <returns> 파싱된 데이터 리스트</returns>
    public static async Task<List<T>> ReadTableAsync<T>(string tableName, bool isStreamingAssetPath = false)
    {
        string basePath = isStreamingAssetPath ? Application.streamingAssetsPath : Application.persistentDataPath;
        string folderPath = Path.Combine(basePath, "Table");
        string filePath = Path.Combine(folderPath, tableName + ".tsv");

        if (File.Exists(filePath) == false)
        {
            Debug.LogError($"[TableLoader] 파일이 존재하지 않습니다: {filePath}");
            return null;
        }

        try
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, TsvConfig);

            var records = new List<T>();
            await foreach (var record in csv.GetRecordsAsync<T>())
            {
                records.Add(record);
            }

            return records;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TableReader] {tableName}.tsv 로딩 실패: {ex.Message}");
            return null;
        }
    }

    public static List<T> ReadTable<T>(string filePath)
    {
        string tableText = ReadAllText(filePath);
        if (tableText == null)
        {
            Debug.LogError($"[TableReader] 파일이 존재하지 않습니다: {filePath}");
            return null;
        }

        try
        {
            using var reader = new StringReader(tableText);
            using var csv = new CsvReader(reader, TsvConfig);

            var records = new List<T>();
            
            foreach (var record in csv.GetRecords<T>())
            {
                records.Add(record);
            }

            return records;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TableLoader] {filePath} 로딩 실패: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 파일 내용을 문자열로 읽습니다. Android의 StreamingAssets(jar:file://)처럼 File API로 접근할 수 없는 경로는 UnityWebRequest로 읽습니다.
    /// </summary>
    /// <returns> 파일 내용, 실패하면 null</returns>
    private static string ReadAllText(string filePath)
    {
        if (filePath.Contains("://") == false)
            return File.Exists(filePath) ? File.ReadAllText(filePath) : null;

        using var request = UnityWebRequest.Get(filePath);
        var operation = request.SendWebRequest();
        while (operation.isDone == false) { }

        if (request.result != UnityWebRequest.Result.Success)
            return null;

        // UTF-8 BOM이 첫 헤더 이름에 섞이지 않도록 제거한다.
        return request.downloadHandler.text.TrimStart('﻿');
    }
}
}
