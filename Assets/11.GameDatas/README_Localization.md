Content authoring guide (KR/EN)

- Character info:
  - Preferred: `Assets/11.GameDatas/Character/{character}/info/{lang}/info.json`
  - Fallback:  `Assets/11.GameDatas/Character/{character}/info.json`
- Character memory files:
  - Preferred: `Assets/11.GameDatas/Character/{character}/memory/{type}/{lang}/{file}`
  - Fallback:  `Assets/11.GameDatas/Character/{character}/memory/{type}/{file}`
- Prompts:
  - Preferred: `Assets/11.GameDatas/prompt/{lang}/{name}`
  - Fallback:  `Assets/11.GameDatas/prompt/{name}`

Languages
- `en` (default), `kr`

Notes
- If a KR file is missing, the system will try EN or root fallback.
- In the editor, use the Entity inspector buttons to copy EN → KR or clear KR.

