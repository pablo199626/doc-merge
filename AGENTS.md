# Document Operator

- El nombre público es `document-operator`; el paquete Python es `document_operator` porque los módulos no admiten guiones.
- Límite de responsabilidades: `cli.py` procesa argumentos; `sorting.py` registra perfiles en `REGLAS_ORDENACION`; `docx_processor.py` modifica el DOCX. Las nuevas reglas van en `sorting.py` y se exponen con `--orden`; no deben modificar el procesador XML.
- El procesador identifica la tabla por la cabecera `Nombre y Apellidos` (ajustable con `--cabecera`) y reordena filas desde la segunda celda. Mueve nodos XML `<w:tr>` completos: nunca reconstruir un `Document`, una tabla ni usar `cell.text`, porque se perderían formato y contenido no tabular.
- Verificación principal: `python -m unittest discover -s tests -v`. Las pruebas usan `PAABLO ANEXOS.docx`, comprueban el orden, que el original no cambia y que solo se modifica `word/document.xml`.
- Ejecutar desde fuente: `python -m document_operator "PAABLO ANEXOS.docx" --force`. Para registrar el comando local: `python -m pip install -e .` y después `document-operator ...`.
- Para crear un `.exe` Windows x64: `python -m pip install -r requirements-dev.txt` y `python build_exe.py`. PyInstaller genera `dist\document-operator.exe`; el artefacto de distribución actual se entrega desde `Document-Operator\` junto con `LEEME.txt`.
- Los artefactos de PyInstaller (`build/`, `dist/`, `*.spec`) y cachés Python no son código fuente. El `.exe` actual se compiló para Windows de 64 bits; para Windows de 32 bits hay que construirlo con Python y PyInstaller de 32 bits.
