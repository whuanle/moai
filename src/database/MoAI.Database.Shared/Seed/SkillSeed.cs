using System;
using Microsoft.EntityFrameworkCore;
using MoAI.Database.Entities;

namespace MoAI.Database.Seed;

/// <summary>
/// 技能种子数据：系统内置技能.
/// <para>
/// 元数据落库；脚本文件以 MoAI.Skill.Core 程序集内嵌资源分发
/// （Resources/skills/{key}/...），加载时写入会话沙箱.
/// </para>
/// </summary>
public static class SkillSeed
{
    /// <summary>
    /// 内置技能：docx_writer.
    /// </summary>
    public static readonly Guid DocxWriterId = new("6a3f2c1e-9b4d-4e8a-8c5f-1d2e3f4a5b6c");

    /// <summary>
    /// 内置技能：ppt_writer.
    /// </summary>
    public static readonly Guid PptWriterId = new("8c7b6a5d-4e3f-4a2b-9d1c-0f9e8d7c6b5a");

    private static readonly DateTimeOffset SeedTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 应用技能种子数据.
    /// </summary>
    /// <param name="modelBuilder">模型构建器.</param>
    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SkillEntity>().HasData(
            new SkillEntity
            {
                Id = DocxWriterId,
                Key = "docx_writer",
                Name = "Word 文档生成",
                Description = "根据用户提供的大纲和内容要求，在沙箱中生成排版良好的 .docx Word 文档（报告/方案/合同等），并返回下载链接。",
                Instructions = DocxWriterInstructions,
                Files = "[]",
                IsSystem = true,
                TeamId = 0,
                IsDisable = false,
                CreateUserId = 0,
                CreateTime = SeedTime,
                UpdateUserId = 0,
                UpdateTime = SeedTime,
                IsDeleted = 0
            },
            new SkillEntity
            {
                Id = PptWriterId,
                Key = "ppt_writer",
                Name = "PPT 演示文稿生成",
                Description = "根据用户提供的大纲和内容要求，在沙箱中生成 .pptx 演示文稿（标题页+内容页），并返回下载链接。",
                Instructions = PptWriterInstructions,
                Files = "[]",
                IsSystem = true,
                TeamId = 0,
                IsDisable = false,
                CreateUserId = 0,
                CreateTime = SeedTime,
                UpdateUserId = 0,
                UpdateTime = SeedTime,
                IsDeleted = 0
            });
    }

    private const string DocxWriterInstructions = """
        # 生成 Word 文档（docx）

        当用户需要生成 Word 文档、报告、方案、说明书等 .docx 文件时使用本技能。

        ## 使用步骤
        1. 将用户需求整理为文档大纲（标题、章节、要点），信息不足时先向用户确认，不要编造关键数据。
        2. 技能脚本已加载到沙箱 `/workspace/skills/docx_writer/` 下，用 sandbox_run_code（python）执行：
           ```python
           exec(open('/workspace/skills/docx_writer/generate_docx.py', encoding='utf-8').read())
           doc = build_docx(
               title='文档标题',
               outline=[
                   {'level': 1, 'text': '第一章 概述', 'children': [
                       {'level': 2, 'text': '1.1 背景', 'paragraphs': ['段落文字……', '支持 **加粗** 标记。']},
                   ]},
               ],
               out_path='/workspace/output.docx')
           ```
           outline 中每一项支持 `level`（1/2/3 标题层级）、`text`（标题文字）、`children`（子级）、`paragraphs`（该节下的正文段落列表）。
        3. 生成完成后调用 sandbox_save_artifact 工具保存 `/workspace/output.docx`，将返回的下载链接发给用户。

        ## 约束
        - 文档内容使用与用户对话一致的语言。
        - 长文档按大纲分章节组织，避免把全部内容塞进一个段落。
        """;

    private const string PptWriterInstructions = """
        # 生成 PPT 演示文稿（pptx）

        当用户需要生成演示文稿、汇报 PPT、课件等 .pptx 文件时使用本技能。

        ## 使用步骤
        1. 将用户需求整理为演示大纲：封面标题 + 每页幻灯片的标题与要点，信息不足时先向用户确认。
        2. 技能脚本已加载到沙箱 `/workspace/skills/ppt_writer/` 下，用 sandbox_run_code（python）执行：
           ```python
           exec(open('/workspace/skills/ppt_writer/generate_pptx.py', encoding='utf-8').read())
           prs = build_pptx(
               title='演示标题',
               subtitle='副标题/汇报人',
               slides=[
                   {'title': '页面标题', 'bullets': ['要点一', '要点二（子要点用缩进空格表示）']},
               ],
               out_path='/workspace/output.pptx')
           ```
           bullets 中以两个空格开头的条目会渲染为上一条的子要点。
        3. 生成完成后调用 sandbox_save_artifact 工具保存 `/workspace/output.pptx`，将返回的下载链接发给用户。

        ## 约束
        - 内容使用与用户对话一致的语言；每页要点建议不超过 6 条，保持简洁。
        - 不生成图片型幻灯片，以标题+要点版式为主。
        """;
}
