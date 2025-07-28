// <copyright file="Program.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.Diagnostics;
using System.Text;

namespace MysqlScaffold;

/// <summary>
/// Program.
/// </summary>
public class Program
{
    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Console.WriteLine("请在目录下执行 dotnet run，请勿直接启动该项目");
        // Console.WriteLine("使用前先删除 Data、Entities 两个目录，用完后也要删除");
        DirectoryInfo? assemblyDirectory = Directory.GetParent(typeof(Program).Assembly.Location);
        if (assemblyDirectory!.FullName.Contains("bin", StringComparison.CurrentCultureIgnoreCase))
        {
            assemblyDirectory = assemblyDirectory!.Parent!.Parent!.Parent;
        }

        string projectDirectory = assemblyDirectory!.FullName;
        Directory.SetCurrentDirectory(projectDirectory);
        Console.WriteLine("当前工作目录: " + projectDirectory);

        if (Directory.Exists(Path.Combine(projectDirectory, "Data")))
        {
            Directory.Delete(Path.Combine(projectDirectory, "Data"), true);
        }

        if (Directory.Exists(Path.Combine(projectDirectory, "Entities")))
        {
            Directory.Delete(Path.Combine(projectDirectory, "Entities"), true);
        }

        var configurationBuilder = new ConfigurationBuilder();
        ImportSystemConfiguration(configurationBuilder);
        var configuration = configurationBuilder.Build();

        var connectionString = configuration["MoAI:Database"];
        if (!connectionString.EndsWith(';'))
        {
            connectionString += ";"; // 确保连接字符串以分号结尾
        }

        connectionString += "GuidFormat=Binary16";

        // 本机已经安装需先安装 dotnet-ef
        // dotnet tool install -g dotnet-ef
        ProcessStartInfo? processStartInfo = new()
        {
            FileName = "dotnet",
            WorkingDirectory = projectDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        processStartInfo.ArgumentList.Add("ef");
        processStartInfo.ArgumentList.Add("dbcontext");
        processStartInfo.ArgumentList.Add("scaffold");
        processStartInfo.ArgumentList.Add($"\"{connectionString.ToString()}\"");
        processStartInfo.ArgumentList.Add("Pomelo.EntityFrameworkCore.MySql");
        processStartInfo.ArgumentList.Add("--context-dir");
        processStartInfo.ArgumentList.Add("Data");
        processStartInfo.ArgumentList.Add("--context");
        processStartInfo.ArgumentList.Add("DatabaseContext");
        processStartInfo.ArgumentList.Add("--output-dir");
        processStartInfo.ArgumentList.Add("Entities");
        processStartInfo.ArgumentList.Add("--namespace");
        processStartInfo.ArgumentList.Add("MoAI.Database.Entities");
        processStartInfo.ArgumentList.Add("--context-namespace");
        processStartInfo.ArgumentList.Add("MoAI.Database");
        processStartInfo.ArgumentList.Add("--no-onconfiguring");
        processStartInfo.ArgumentList.Add("-f");

        ////processStartInfo.Arguments = string.Join(" ", processStartInfo.ArgumentList);
        ////processStartInfo.ArgumentList.Clear();
        string? command = $"{processStartInfo.FileName} {string.Join(" ", processStartInfo.ArgumentList)}";
        Console.WriteLine($"启动命令: {command}");

        using Process? process = new() { StartInfo = processStartInfo };

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.WriteLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Data);
                Console.ResetColor();
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            var entities = Directory.GetFiles(Path.Combine(projectDirectory, "Entities"), "*.cs", SearchOption.AllDirectories);
            foreach (var entity in entities)
            {
                var newFileNamee = Path.Combine(Directory.GetParent(entity)!.FullName, Path.GetFileNameWithoutExtension(entity) + "Entity.cs");
                File.Move(entity, newFileNamee);
            }
        }
    }

    // 导入系统配置.
    private static void ImportSystemConfiguration(IConfigurationBuilder configurationBuilder)
    {
        // 指定环境变量从文件导入配置
        var configurationFilePath = Environment.GetEnvironmentVariable("MAI_FILE");
        if (string.IsNullOrWhiteSpace(configurationFilePath))
        {
            throw new ArgumentException("The environment variable `MAI_FILE` is not set or is empty. Please set it to the path of the configuration file.");
        }

        string? fileType = Path.GetExtension(configurationFilePath);
        if (".json".Equals(fileType, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            configurationBuilder.AddJsonFile(configurationFilePath);
        }
        else if (".yaml".Equals(fileType, StringComparison.OrdinalIgnoreCase))
        {
            configurationBuilder.AddYamlFile(configurationFilePath);
        }
        else if (".conf".Equals(fileType, StringComparison.OrdinalIgnoreCase))
        {
            configurationBuilder.AddIniFile(configurationFilePath);
        }
        else
        {
            throw new ArgumentException($"The current file type cannot be imported,`MAI_FILE={configurationFilePath}`.");
        }
    }
}