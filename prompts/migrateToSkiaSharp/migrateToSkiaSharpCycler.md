Run automatic refactor rounds until convergence, executing build error detection and per-file session orchestration in a single flow.

Data:
- project: E:\NProjects\a1f.webapi\
- targets_path: .\tools\error-files.csv
- template_path: .\.github\prompts\migrateToSkiaSharpByFile.prompt.md
- placeholder_file: {{target_file}}
- placeholder_lines: {{target_lines}}
- batch_size: 4
- kickoff_mode: autopilot
- notify_on_idle: once
- coordinate_with_creator: false
- build_errors_cmd: .\tools\build-errors.ps1 .\src\a1f.webapi\a1f.webapi.csproj
- max_rounds: 20

Algorithm:
1. round = 1
2. While round <= max_rounds:
   a. From repository root, run:
      .\tools\build-errors.ps1 .\src\a1f.webapi\a1f.webapi.csproj
   b. If build-errors fails -> stop with error.
   c. If targets_path does not exist -> stop with error.
   d. Read targets_path as CSV (columns: File, Lines), clean up empty/duplicate rows by File.
   e. If the list is empty -> report CONVERGED and stop.
   f. **FAN-OUT (mandatory):** For each row (File, Lines) in the list, spawn all sessions concurrently in batches of batch_size. Do NOT process files sequentially — create all sessions in the current batch before waiting for any of them to finish.
      - Generate prompt replacing {{target_file}} with File and {{target_lines}} with Lines.
      - Create 1 session per file (name: "Refactor <File>").
      - Launch the full batch simultaneously, then wait for the batch to complete before launching the next batch.
   g. Wait for all sessions in this round to finish.
   h. Report per-round table: file | lines | session_id | final status.
   i. Report round summary: round | files processed | sessions | status.
   j. round = round + 1
3. If max_rounds is reached without CONVERGED, stop and report remaining pending files.

Rules:
- Stop immediately if build-errors fails.
- Do not create sessions if build-errors fails.
- Continue automatically between rounds without user intervention.
- All necessary reads and file modifications within the project folder are allowed without requesting authorization.
- Do not edit files outside the scope of each session.
