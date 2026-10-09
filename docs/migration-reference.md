# Referencia de migración a C#

Estado registrado el 8 de octubre de 2026 para la tarea T00.

Este documento identifica la implementación Python que actúa como referencia de
comportamiento durante la migración. No autoriza a modificarla ni sustituye a
`AGENTS.md`.

## Identidad de la referencia

- Repositorio: `C:/AI/Projects/ConsultantProject`.
- Rama: `master`.
- Commit base: `032cf9c33670ccabb2838faa5329f1f269252f2b`.
- Referencia autorizada: árbol de trabajo actual, incluidos los archivos sin
  seguimiento enumerados más abajo.
- Manifiesto SHA-256 del código y fixtures: `docs/migration-reference.sha256`.

El commit base no contiene por sí solo la referencia completa. Hay cambios
locales y módulos sin seguimiento que forman parte del comportamiento aprobado.
No se debe reconstruir la referencia aplicando únicamente `git checkout` sobre
ese commit.

## Estado del árbol en T00

Cambios en archivos conocidos por Git:

```text
 M AGENTS.md
 D Document-Operator.zip
 D Document-Operator/LEEME.txt
 D Document-Operator/PAABLO ANEXOS.docx
 M Document-Operator/document-operator.exe
 M README.md
 M document_operator/__init__.py
 M document_operator/cli.py
 M document_operator/docx_processor.py
 M document_operator/sorting.py
 M pyproject.toml
 M requirements-dev.txt
 M tests/test_document_operator.py
```

Contenido sin seguimiento relevante:

```text
ANEXOS_4ESO_A.docx
Document-Operator/REEDME.txt
LISTA_NOMBRES.pdf
docs/contrato-transferencia.md
document-operator-vs/REEDME.txt
document-operator-vs/document-operator.exe
document_operator/identity.py
document_operator/matching.py
document_operator/pdf_reader.py
document_operator/transfer.py
document_operator/transfer_types.py
document_operator/validation.py
tests/fixtures/transferencia/casos.json
tests/fixtures/transferencia/pagina_pdf.txt
tests/test_cli.py
tests/test_code_quality.py
tests/test_docx_reader.py
tests/test_matching.py
tests/test_pdf_reader.py
tests/test_sorting.py
tests/test_transfer.py
```

También existe `document_operator/.idea/`, que es configuración local del IDE y
no forma parte de la implementación de referencia.

## Entorno Python observado

| Elemento | Valor |
| --- | --- |
| Ejecutable | `C:/Users/PC GAMING/AppData/Local/Programs/Python/Python312/python.exe` |
| Implementación | CPython 3.12.10 |
| Unicode | 15.0.0 |
| Sistema | Windows 11, compilación 10.0.26200, x64 |
| `pypdf` | 6.19.0 |
| PyInstaller | 6.22.3 |
| Codificación stdout/stderr | `cp1252` |
| Codificación del sistema de archivos | UTF-8 |

`pyproject.toml`, `document_operator.__version__` y `--version` indican 2.1.0.
Los metadatos de la instalación editable y `pip freeze` indican todavía 1.0.0.
Esta divergencia se considera metadato local obsoleto, no una versión funcional
alternativa.

## Verificación inicial

Comando ejecutado:

```powershell
python -B -m unittest discover -s tests -v
```

Resultado: 30 pruebas ejecutadas y 30 superadas en 0,294 segundos.

También se verificó:

```text
python -B -m document_operator --version
document-operator 2.1.0
```

La ayuda se genera correctamente, pero la captura realizada por el entorno de
automatización mostró caracteres españoles sustituidos. Python informó `cp1252`
para stdout y stderr. Las futuras pruebas por proceso deben fijar expresamente
la codificación antes de comparar bytes o texto.

## Archivos necesarios para reproducir Python

El archivo de la referencia deberá incluir, conservando su contenido:

- `document_operator/*.py`.
- `tests/*.py`.
- `tests/fixtures/transferencia/`.
- `pyproject.toml`.
- `requirements-dev.txt`.
- `build_exe.py`.
- `document-operator.spec`, como evidencia del empaquetado observado.
- `README.md` y `docs/contrato-transferencia.md` como contratos documentados.

No se deben archivar como código fuente:

- `__pycache__/` y archivos `*.pyc`.
- `build/`, `dist/` ni otros artefactos generados.
- `document_operator.egg-info/`, porque contiene metadatos obsoletos.
- `document_operator/.idea/`.
- Documentos reales ni ejecutables distribuidos.

## Dependencias de rutas

- `tests/test_document_operator.py` resuelve
  `PAABLO ANEXOS.docx` desde la raíz situada un nivel por encima de `tests/`.
  Es la única prueba que depende directamente de un documento real en la raíz.
- `tests/test_matching.py` resuelve
  `tests/fixtures/transferencia/casos.json` de forma relativa al archivo de
  prueba.
- `tests/test_pdf_reader.py` resuelve
  `tests/fixtures/transferencia/pagina_pdf.txt` de forma relativa al archivo de
  prueba.
- `tests/test_code_quality.py` inspecciona `document_operator/*.py` desde la raíz
  situada un nivel por encima de `tests/`.
- `build_exe.py` y `document-operator.spec` esperan
  `document_operator/__main__.py` desde el directorio de trabajo del proyecto.

La tarea de archivo debe conservar estas relaciones o proporcionar un arnés
externo que las reproduzca. No debe editar las pruebas Python para ocultar una
ruta rota.

## Módulos de referencia

| Módulo | Responsabilidad observable |
| --- | --- |
| `identity.py` | NFC, espacios y `casefold` para identidad exacta |
| `sorting.py` | Interpretación heredada y dos perfiles de ordenación |
| `pdf_reader.py` | Extracción, validación y trazabilidad del listado PDF |
| `docx_processor.py` | Lectura, ordenación e importación de filas y recursos |
| `matching.py` | Plan conservador de acciones e incidencias |
| `validation.py` | Integridad del resultado y de las entradas |
| `transfer.py` | Flujo temporal, validación y publicación |
| `cli.py` | Argumentos, mensajes y asistente interactivo |

## Contratos observados que deben caracterizarse

El CLI actual expone el argumento `source` y las opciones `-o`, `--output`,
`--destino`, `--lista`, `--simular`, `--orden`, `--cabecera`, `--force`,
`--version`, `-h` y `--help`.

Hay dos comportamientos DOCX distintos que no deben unificarse durante el
portado inicial:

- La ordenación simple elige la primera tabla cuya celda contiene el texto de
  cabecera y usa siempre la segunda columna como nombre.
- La transferencia exige una única cabecera exacta normalizada y deriva de ella
  la columna de identidad.

La corrección para unificarlos está autorizada únicamente como fase funcional
separada, después de certificar la paridad.

Los 11 casos de aceptación de correspondencias son:

```text
reemplazar
anadir_y_eliminar
origen_ausente_conservar
ausente_en_ambos
origen_ambiguo_conservar
origen_ambiguo_sin_destino
destino_ambiguo_continuar
destino_ambiguo_ausente_pdf
normalizacion_y_duplicado_pdf
no_confundir_tildes
medidas_vacias_reemplazan
```

## Documentos locales y artefactos

Estos documentos son privados y solo pueden abrirse para lectura. En el
inventario T00 estaban en la raíz; actualmente se conservan bajo
`assets/documents/`:

| Ruta | Tamaño | SHA-256 |
| --- | ---: | --- |
| `assets/documents/PAABLO ANEXOS.docx` | 71821 | `a4a8430f2319d689eb2c5217a954538c50f6800cf8420a873fd3952acf9ebd02` |
| `assets/documents/ANEXOS_4ESO_A.docx` | 72834 | `1c0d0cd12436a9bfaa2b62d0c8e42203163f9c19158072787fe31764a613dcfb` |
| `assets/documents/LISTA_NOMBRES.pdf` | 2250 | `80dc0ada15552cba979f58440e93b4f5063940cbe107753dc853e371a0af45da` |

Los hashes se comprobaron antes y después de la suite y no cambiaron.

Los ejecutables presentes en `dist/`, `Document-Operator/` y
`document-operator-vs/` eran idénticos en T00:

```text
SHA-256: 120223261fe855f612a277a4169234e511f662e58e91cf24d188b8e2a7d14344
Tamaño: 10873022 bytes
```

Los dos archivos `REEDME.txt` presentes también son idénticos:

```text
SHA-256: 71a10c426cb4b07ec6df63df84fe90e44cbe34dd62e049bf2687e4dbb7cab276
Tamaño: 1621 bytes
```

En T00, la distribución registrada por Git ya había perdido `LEEME.txt`, el
documento incluido y el ZIP anterior. Durante la migración no se modificaron
como efecto secundario.

Tras completar la migración, los artefactos intermedios y las entregas
duplicadas se retiraron. La única entrega conservada está en `release/`.

El 9 de octubre de 2026, la implementación C# pasó a denominarse `DocMerge`.
Los nombres de la referencia Python se conservaron intactos como evidencia
histórica. La nueva entrega es `release/DocMerge.exe`, con SHA-256
`c374e28de1d615012ae0db2ba395442537043c2f28bc5ad4a045a7f2481127ff`.

## Riesgos ya confirmados

1. El árbol actual no puede reconstruirse solo desde el commit base.
2. Una prueba automática depende de un documento real.
3. La instalación editable tiene metadatos de versión obsoletos.
4. La codificación de consola puede alterar capturas textuales.
5. La ordenación simple y la transferencia aplican contratos DOCX distintos.
6. Los artefactos generados y los ajustes del IDE no están completamente
   cubiertos por el `.gitignore` actual.

Estos puntos se corrigen o aíslan en tareas posteriores; T00 solo los registra.
