# Taste
- Communicates in Simplified Chinese (UI copy, feature descriptions, and labels are written in zh-CN). Confidence: 0.8
- Prefers flat/inline list layouts over dropdown selects for filters and options (e.g., a leftmost refresh button followed by the category filter tags laid out in a horizontal row, with `|` separators between options rather than pill/tag styling). Confidence: 0.8
- Prefers icon-only toolbar buttons with a tooltip over text-labeled buttons for header/management actions (e.g., the category-manage button). Confidence: 0.65
- Prefers using Tabs to separate different category/type groups within a management UI (e.g., plugin / app / kb as separate Tabs). Confidence: 0.7
- Prefers management/administration modules to be restricted to admin users only (admin-only access gating). Confidence: 0.7
- When migrating functionality from an old/reference project, prefers to refactor and adapt it into the new architecture rather than copying it verbatim (e.g., "不要完全照搬，我们是重构项目"). Confidence: 0.85
- Prefers centralized fixed string constants (single source of truth) for shared enumerated/type values across modules, so other modules reference the shared constant instead of hand-writing their own literals (e.g., a `ClassifyTypes` constants class for plugin/app/kb). Confidence: 0.9
- Prefers to get a working version built first and then iterate on the details rather than perfecting specs upfront (e.g., "你先做出来，细节我们慢慢再调整"). Confidence: 0.65
