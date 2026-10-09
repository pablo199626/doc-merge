# Document Operator

Ordena las filas de una tabla Word y puede generar un informe a partir de un
DOCX de origen, un DOCX de destino y una lista PDF. No reconstruye tablas:
mueve filas XML completas y conserva el formato y la información asociados.

## Uso

En Windows también se puede hacer doble clic en `document-operator.exe` y seguir
el asistente interactivo. Las rutas pueden escribirse o introducirse arrastrando
los archivos a la ventana; esta permanece abierta al terminar.

```powershell
document-operator "PAABLO ANEXOS.docx"
document-operator "PAABLO ANEXOS.docx" -o resultado.docx
document-operator "PAABLO ANEXOS.docx" --orden nombre-apellidos
```

La regla predeterminada es `apellido-nombre-segundo-apellido`. La tabla se
identifica por la cabecera `Nombre y Apellidos`; se puede cambiar con
`--cabecera "Otro texto"`.

## Transferencia mediante una lista PDF

```powershell
document-operator "ORIGEN.docx" --destino "DESTINO.docx" --lista "LISTA.pdf"
document-operator "ORIGEN.docx" --destino "DESTINO.docx" --lista "LISTA.pdf" -o "RESULTADO.docx"
document-operator "ORIGEN.docx" --destino "DESTINO.docx" --lista "LISTA.pdf" --simular
```

La salida predeterminada es `DESTINO_generado.docx`. El PDF determina las
personas, el origen aporta sus filas completas y su formato, y el destino aporta
la estructura general del informe. El resultado se ordena y valida antes de
publicarse. Ninguna entrada se modifica. Las identidades ausentes o ambiguas se
gestionan de forma conservadora y se explican al finalizar.

## Desarrollo

### Arquitectura de transferencia

La fase 1 de análisis está recogida en
[`docs/contrato-transferencia.md`](docs/contrato-transferencia.md), con las reglas
de identidad, conservación de formato y casos de aceptación ficticios en
`tests/fixtures/transferencia/`. Las ocho fases están implementadas; ejecutar
siempre la batería completa antes de compilar una distribución.

```powershell
python -m pip install -e .
python -m document_operator --help
python -m document_operator "PAABLO ANEXOS.docx" --force
```

## Crear el ejecutable Windows

```powershell
python -m pip install -r requirements-dev.txt
python build_exe.py
```

El resultado se crea en `dist\document-operator.exe`. No requiere que la
persona usuaria tenga Python instalado:

```powershell
.\document-operator.exe "PAABLO ANEXOS.docx"
```
