# Talos / DocMerge — Instrucciones del repositorio

Este archivo define **contratos y restricciones específicos de Talos**. El framework multiagente global de OpenCode gestiona orquestación, agentes, modelos, skills, comandos y permisos. **No dupliques ni reconfigures ese framework desde aquí.** Lee solo las referencias necesarias para la tarea.

## 1. Modo de trabajo y comunicación

- Responde y presenta resultados al usuario **en español**; mantén identificadores de código en inglés.
- Prioriza cambios pequeños, directamente vinculados a la petición. Reutiliza el código existente; no añadas capas, abstracciones, dependencias ni refactors no solicitados.
- Respeta el modo/comando recibido (`/plan-task`, `/feature`, `/fix`, etc.). En modo planificación **no implementes**. Para tareas sencillas, no movilices todos los agentes ni hagas investigaciones extensas salvo que exista un riesgo concreto.
- Pide aclaraciones solo cuando falte una decisión que altere funcionalidad, compatibilidad o datos; no bloquees por preferencias menores de implementación.
- **Respuesta final breve por defecto (2–5 líneas):** qué cambió, qué se verificó y, solo si aplica, riesgo o pendiente. No incluyas diarios de trabajo, explicaciones archivo por archivo, logs completos, listas de agentes usados ni propuestas de mejoras no solicitadas. Amplía solo si el usuario lo pide, la tarea es una planificación, hay un fallo o existe un riesgo relevante.
- Nunca afirmes que una prueba, compilación o revisión visual pasó si no se ejecutó. Comunica impedimentos de forma concisa.

## 2. Verificación proporcional al cambio

**No ejecutes automáticamente `dotnet test` completo, `dotnet build` completo y `dotnet format` completo después de cada tarea.** Escoge el mínimo que dé confianza suficiente, según el impacto real:

| Cambio | Verificación habitual |
| --- | --- |
| Markdown, comentarios o documentación sin efecto ejecutable | Revisar el contenido y `git diff --check`. **Sin build ni tests.** |
| Ajustes puramente visuales (XAML, estilos, colores, iconos, espaciado) | `git diff --check`; compilar **WPF** si cambia XAML/recursos; comprobación visual puntual si es viable. **Sin suite completa.** |
| Corrección o funcionalidad localizada | Compilar el proyecto afectado y ejecutar **tests focalizados** del comportamiento cambiado. Verificar formato de los archivos C# modificados, no toda la solución salvo necesidad. |
| Cambio transversal, contratos públicos, identidad, matching, PDF/DOCX, integridad, publicación o distribución | Pruebas de las capas implicadas; **suite completa** y comprobaciones de integridad/paridad cuando el riesgo o el alcance lo justifiquen. |
| Publicación solicitada | Además de lo anterior, publicar y verificar artefacto aislado, versiones, funcionamiento e integridad. |

- Los cambios de lógica nuevos requieren tests de comportamiento pertinentes; las correcciones de bugs llevan regresión cuando sea reproducible y razonable. Usa **xUnit** y fixtures ficticios, aislados y deterministas.
- Prioriza el **test más específico disponible** y amplía solo cuando fallen pruebas, se compartan contratos o haya indicios de impacto más amplio. No repitas verificaciones ya superadas sin cambios posteriores relevantes.
- `dotnet format --verify-no-changes`, `dotnet build -warnaserror` y `dotnet test` siguen siendo comprobaciones válidas de solución completa, **no obligaciones universales**. Úsalas cuando sean apropiadas, cuando el usuario las solicite o antes de una entrega de riesgo.
- Para problemas de UX, la compilación no sustituye la inspección visual; si no puedes abrir la aplicación, indica esa limitación y no des por validado el aspecto.
- Si no se puede ejecutar una comprobación necesaria, explica brevemente cuál y por qué. **No instales herramientas ni cambies versiones para solucionar el entorno sin autorización.**

## 3. Producto, estructura y límites

- **Talos** es el producto comercial de escritorio; `DocMerge` se conserva como nombre técnico, raíz de namespaces y CLI. Plataforma: **Windows x64, .NET 10**, WPF/MVVM. SDK fijado por `global.json`.
- Solución: `DocMerge.Domain` (reglas), `DocMerge.Application` (casos de uso), `DocMerge.Infrastructure` (PDF/DOCX/archivos), `DocMerge.Console` (CLI) y `DocMerge.Wpf` (interfaz). Tests correspondientes en `tests/`.
- Las dependencias apuntan hacia Domain; no introduzcas tipos de WPF, `MessageBox` ni `Console.*` en Domain/Application/Infrastructure. `Console.*` solo corresponde a Console. Las operaciones de entrada/salida y largas deben ser asíncronas y cancelables cuando proceda.
- WPF consume Application, nunca invoca la consola como subproceso. Mantén los temas Sistema/Claro/Oscuro, el comportamiento adaptable, la accesibilidad, el foco, los filtros y la virtualización. No reconstruyas el plan por cambios de presentación.
- **No cambies nombres técnicos, versiones, contratos externos ni dependencias sin autorización.** Versiones actuales documentadas: CLI `2.1.0`; identidad comercial WPF `Talos 1.0.0`. Usa `.editorconfig` y `Directory.Build.props` como fuentes de configuración.
- `WPF-UI` `4.3.0`, `PdfPig` `0.1.16` y `Microsoft.Extensions.DependencyInjection` `10.0.12` están fijados. El DOCX usa `System.IO.Compression` y `System.Xml.Linq`: no añadas Open XML SDK ni otra biblioteca sin aprobación.

## 4. Contratos funcionales que no se pueden romper

- `legacy/python/` es un **oráculo histórico inmutable**, no código activo. Mantén paridad en operaciones heredadas cuando estén afectadas; no ejecutes siempre la paridad completa para cambios ajenos.
- Preserva opciones, mensajes y semántica pública del CLI descritos en `docs/migration-reference.md`, `README.md` y `docs/class-generation.md`, incluida la cabecera `Nombre y Apellidos` y la sección PDF `Alumnado (n)` cuando corresponda.
- **Identidad exacta:** Unicode FormC, espacios y *full case folding* Unicode 15.0 según implementación propia. No sustituir por `ToLowerInvariant`, eliminación de acentos ni heurísticas culturales.
- **Generación de clases:** los DOCX históricos forman un índice global; el PDF determina pertenencia a clase y NIA. `StudentNameMatchKey` es una clave tolerante distinta de la identidad exacta; nunca se elige arbitrariamente entre candidatos ambiguos.
- **Estados:** `Found` = único histórico e incluido; `NewStudent` = sin histórico e incluido con campos restantes inicialmente vacíos; `Conflict` = ambiguo y **omitido**. Los conflictos no bloquean por sí solos el lote. Los NIA duplicados y las clases duplicadas son bloqueos estructurales. Clases sin incluidos no generan DOCX; un lote sin documentos no se publica.
- **Estilo semántico:** `Found` verde (`Success*`), `NewStudent` azul (`Information*`), `Conflict` rojo (`Error*`). Los avisos independientes usan `Warning*`; los colores del contenido documental no se reinterpretan.
- Las búsquedas, filtros, badges, contadores y panel de detalle son **solo presentación**: no cambian matching, plan, inclusión, totales ni resultado. Conservar selección, foco y scroll durante navegación y cambios visuales.
- **DOCX:** preserva el WordprocessingML y sus recursos (estilos, numeración, relaciones) para los datos no modificados. No reconstruyas filas/documentos desde texto plano. La tabla y plantilla deben cumplir los contratos de estructura de `docs/class-generation.md`; el nombre visible procede del PDF.
- La lectura/revisión de medidas enriquecidas y cualquier edición futura deben respetar la correspondencia con las **celdas de origen**; una vista conjunta no autoriza fusionar ni sobrescribir celdas. El contenido incompatible no se pierde silenciosamente.
- **Integridad:** originales privados siempre intactos. Genera en temporales, valida y publica atómicamente en una carpeta nueva; verifica que el plan preparado no está obsoleto. Una operación cancelada o fallida no debe dejar resultados parciales publicados.

## 5. Código, privacidad y entregas

- C# e identificadores internos **en inglés**, con nombres legibles; APIs y textos externos en español cuando lo establezca el contrato. Respeta el casing de C#, nombres `Async`, nulabilidad y warnings como errores.
- Código cohesionado, simple y testeable. Prefiere *guard clauses*, composición, DI explícita e inmutabilidad donde aporten valor. No introduzcas acoplamiento UI ↔ lógica documental ni efectos secundarios ocultos.
- Trata errores explícitamente; no ocultes excepciones. Libera recursos; conserva la excepción original como causa. Los tests verifican resultados observables, no detalles irrelevantes de implementación.
- Usa datos **ficticios** para tests, capturas, logs y ejemplos. `assets/documents/` y `assets/outputs/` pueden contener datos privados: son locales/ignorados, no se copian a Git, tests ni entregas. No alteres los originales. Las capturas locales en `assets/UX/` son referencias; no las versiones ni distribuyas sin permiso.
- No realices `publish`, cambios de versión, modificación de `release/` o `release-desktop/`, instalación de paquetes, commit, push, PR o merge **salvo que la tarea o los permisos del framework lo autoricen**. Si se solicita publicación, sigue la verificación de entrega descrita en `README.md` y documentación vigente.
- En Windows, si `dotnet` apunta al runtime x86 sin SDK, usa `C:\Program Files\dotnet\dotnet.exe`; no cambies la plataforma accidentalmente.

## 6. Documentación de referencia: consultar bajo demanda

- [`docs/class-generation.md`](docs/class-generation.md): contratos de preparación, lectura PDF, generación DOCX, validación, salida y WPF.
- [`docs/migration-reference.md`](docs/migration-reference.md): paridad histórica, comportamiento certificado y garantías de migración.
- [`docs/glossary.md`](docs/glossary.md): terminología de negocio y equivalentes técnicos.
- [`README.md`](README.md): comandos de uso, ejecución y distribución.
- Historial de commits de Git: certificaciones antiguas, hashes y cronología de versiones. **No reintroduzcas esa cronología en las instrucciones activas.**

Lee los documentos de referencia **solo cuando sean pertinentes**; no cargues todos los documentos ni ejecutes todas las herramientas por defecto.
