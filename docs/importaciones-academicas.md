# Importaciones académicas

## Planes analíticos en ZIP (Administrador)

En **Mantenimiento del catálogo → Importar planes analíticos desde ZIP**, elige la carrera y una malla **Borrador**, carga el ZIP y pulsa **Analizar ZIP**. Revisa las asociaciones existentes o la creación de asignaturas, el nivel, los créditos, las horas y los contenidos. Los campos se pueden corregir antes de confirmar.

Solo se guardan los planes seleccionados. Se crean los niveles faltantes. Los PDF de otra carrera o ilegibles quedan desmarcados con su error. Un reemplazo de plan existente requiere autorización explícita. Repetir el mismo archivo no duplica las asignaturas. La confirmación completa se guarda en una transacción.

Límites: ZIP 30 MB; hasta 200 PDF, 10 MB por PDF, 150 MB descomprimidos. Se admite el formato textual FISEI del ejemplo; los documentos escaneados requieren OCR previo. El lector reúne contenidos distribuidos en varias páginas.

## Registro y calificaciones desde PDF (Administrador o Profesor)

Desde **Estudiantes → Registrar desde PDF / calificaciones externas** se puede crear al estudiante con sus calificaciones, sin registrarlo antes. También se mantiene la operación de actualizar calificaciones de un estudiante existente.

1. Selecciona el origen: **Universidad Técnica de Ambato** u **Otra universidad / extranjero (extracción con IA)**.
2. Carga un PDF textual de hasta 10 MB y pulsa **Leer récord académico**.
3. Revisa identificación, nombres y apellidos, institución y carrera. Los datos desconocidos quedan editables. La separación de nombres y apellidos debe confirmarse.
4. Para UTA, se propone la carrera por nombre único y la malla vigente cuando es inequívoca; se asocian las materias por nombre exacto normalizado. Resuelve asociaciones pendientes y verifica la versión de malla. El nivel sugerido corresponde al mayor nivel de las materias asociadas, no es una afirmación del nivel actual.
5. Para externos, revisa también las evidencias, el estado y la escala de calificación. Una nota solo se convierte proporcionalmente a 0–10 cuando la escala está expresamente disponible; si no, se deja pendiente. El responsable debe confirmar la equivalencia institucional o editar la nota equivalente.
6. Confirma las materias aprobadas seleccionadas. La creación del estudiante y su historial se realiza en una sola operación en Students. Si ya existe esa identificación, se reutiliza el registro y se conservan sus materias anteriores. Cambiar una nota o período existente requiere autorización de reemplazo.

El formato completo UTA se interpreta por columnas, para separar créditos, período, folio, matrícula y nota. Se procesan páginas de continuación sin encabezado y períodos partidos en dos líneas. Las materias sin cursar permanecen visibles mediante **Mostrar también materias sin cursar, reprobadas o pendientes**, desmarcadas por defecto. No se inventan notas para ellas. El ejemplo `155138.pdf` contiene 43 materias, de las que 15 están aprobadas y suman 45 créditos. También se admite el formato anterior de certificación de un período.

Para internos, se conservan los créditos presentes en el PDF; los créditos u horas ausentes se recuperan del catálogo. Las diferencias entre créditos del documento y del catálogo generan advertencias para revisar la versión de malla. Ambos campos se pueden corregir antes de guardar. Para externos, créditos, horas, institución, carrera o contenidos desconocidos pueden permanecer pendientes en el registro; el análisis de homologación seguirá bloqueado hasta completarlos. Identificación, nombres, apellidos y las notas/períodos de las materias seleccionadas sí deben confirmarse antes de registrar.

### Extracción de documentos de otras universidades

Documents extrae el texto del PDF con PdfPig y lo envía al Agent IA, que utiliza el modelo de lenguaje de Ollama configurado en `Ollama:LanguageModel` (por defecto `qwen2.5:7b`). Se realiza una sola petición, con salida JSON y temperatura 0. El documento se trata como datos, no como instrucciones. No se infieren horas a partir de créditos ni contenidos académicos desde los nombres de asignaturas.

Cada fila requiere evidencia literal verificable en el documento. Se descartan filas sin esa evidencia; las notas que no aparecen en ella quedan pendientes. La escala debe estar expresamente declarada; una nota original se muestra junto a su conversión propuesta para revisión. Los estados de aprobación no respaldados por la evidencia quedan pendientes. La extracción es un apoyo: el usuario confirma o corrige los datos antes de guardar.

Límite de texto para la extracción con IA: 40.000 caracteres; dividir documentos mayores. El límite de lectura PDF es 80 páginas. El tiempo máximo de Ollama es 180 segundos; Documents espera hasta 190 segundos y el Gateway hasta 210 segundos para estas rutas. No hay truncamiento silencioso. Si Ollama falla o su respuesta no es válida, se abre el formulario editable y se puede reintentar o añadir materias manualmente. Las advertencias señalan que la extracción no se completó. Los PDF escaneados requieren OCR previo; este cambio no incorpora OCR ni extracción desde imágenes, DOCX o formatos arbitrarios.

La lectura no valida la firma electrónica. Este cambio **no crea un catálogo de universidades/mallas externas ni carga sus planes analíticos**. Los temas externos siguen completándose desde el historial existente hasta definir ese módulo.

## Contenidos externos y datos pendientes

La hoja **Contenidos** del XLSX conserva la relación `CodigoAsignatura → Tema → Subtema`. Varias filas del mismo tema se agrupan. Se almacenan en Students junto con cada asignatura cursada y se envían al Agent IA al evaluar, sin exigir un ID de asignatura interna.

Se puede guardar un historial externo pendiente. La respuesta del estudiante incluye `informacionFaltante` y `listoParaHomologar`. Si faltan institución, carrera, asignaturas, créditos, horas, temas o subtemas, el expediente muestra los faltantes y el enlace para completarlos. **Evaluar por reglas** y **Analizar con IA** quedan deshabilitados; el backend responde 422 con `faltantes` si se intenta analizar directamente, sin guardar ejecución ni cambiar el estado.

Los externos importados antes de esta mejora tendrán contenidos vacíos: reimporta el XLSX con su hoja Contenidos o complétalos desde el historial.

## Endpoints y despliegue

| Servicio | Endpoint POST | Entrada |
|---|---|---|
| Academic | `/api/v1/catalogo/mallas/{id}/planes-analiticos/analizar` | Multipart `archivo` ZIP |
| Academic | `/api/v1/catalogo/mallas/{id}/planes-analiticos/confirmar` | Multipart `archivo` y `datos` JSON de entradas |
| Documents | `/api/v1/documentos/records/analizar` | Lectura UTA de compatibilidad |
| Documents | `/api/v1/documentos/records/analizar-registro?externo=false` | Multipart `archivo` PDF UTA |
| Documents | `/api/v1/documentos/records/analizar-registro?externo=true` | Multipart `archivo` PDF de otra universidad; extracción IA |
| Documents | `/api/v1/documentos/records/registrar` | Multipart `archivo` y `datos`: documento revisado, tipo, carreraOrigenId, mallaOrigenId y nivelActual |
| Documents | `/api/v1/documentos/records/confirmar` | Multipart `archivo` y `datos` JSON con estudianteId, identificacion y filas; `?externo=true` para PDF externo |

Las rutas están publicadas en el Gateway con Bearer. Documents consulta Students, Academic y Agent IA usando el JWT del usuario. El endpoint de extracción del agente es `POST /api/v1/agent-ia/extraer-record`, con texto JSON y autorización Administrador/Profesor; Documents lo consume directamente. Docker configura `AcademicService__BaseUrl=http://academic:5193` y `AgentIaService__BaseUrl=http://agent-ia:5197` en Documents; fuera de Docker se usan los puertos locales 5193 y 5197 por defecto.

Aplicar las migraciones de Students `ExternalSubjectContents` y `RecordCourseOrigin` antes de usar la carga. En Development se aplican al iniciar; en otros entornos:

```powershell
dotnet ef database update --project backend/Services/Students
```

## Verificación

```powershell
dotnet test backend/HomologacionesFISEI.sln -c Release
cd frontend
npm run build
npm test -- --run
```

Las pruebas cubren registro desde PDF, extracción IA con Ollama simulado, evidencia verificable, escalas desconocidas o 20/100, respaldo manual cuando falla Ollama, lectura de múltiples períodos/carreras, identidad, preservación del historial, temas externos y rechazo de información incompleta; ZIP con rutas inválidas, contenidos de varias páginas, carreras incompatibles, mallas publicadas y reintento sin duplicación; rutas autenticadas y formularios de importación.
