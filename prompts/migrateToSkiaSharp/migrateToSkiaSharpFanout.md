Execute a single refactor round: read the pending files list, and launch one session per file.

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

Algorithm:
1. If targets_path does not exist -> stop with error.
2. Read targets_path as CSV (columns: File, Lines), clean up empty/duplicate rows by File.
3. If the list is empty -> report CONVERGED and stop.
4. **FAN-OUT (mandatory):** For each row (File, Lines) in the list, spawn all sessions concurrently in batches of batch_size. Do NOT process files sequentially — create all sessions in the current batch before waiting for any of them to finish.
   - Generate prompt replacing {{target_file}} with File and {{target_lines}} with Lines.
   - Create 1 session per file (name: "Refactor <File>").
   - Launch the full batch simultaneously, then wait for the batch to complete before launching the next batch.
5. Wait for all sessions in this round to finish.
6. Report table: file | lines | session_id | final status.
7. Return outcome: CONVERGED if the list was empty, PENDING otherwise.

Rules:
- Do not edit files outside the scope of each session.
- All necessary reads and file modifications within the project folder are allowed without requesting authorization.
