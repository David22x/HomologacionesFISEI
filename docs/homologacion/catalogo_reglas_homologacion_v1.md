# Catálogo de reglas de homologación v1

**HU-S3-12 · T12.1 · Responsable: David Cuenca**  
**Estado: pendiente de validación con el docente**

El [JSON versionado](reglas_homologacion_v1.json) reúne las reglas y parámetros del Sprint 3. El motor en C# calcula el resultado académico. El profesor registra la decisión final. Cada ejecución debe conservar la versión del catálogo y los parámetros efectivos.

## Reglas y ejemplos

| ID | Regla | Criterio | Ejemplo | Estado |
| --- | --- | --- | --- | --- |
| R1 | Créditos | Dividir los créditos de origen entre los créditos de destino. Cumple si la razón alcanza el mínimo inclusivo. Si es inferior, emitir Rechazo por créditos, incluso si la cobertura es suficiente. | 4 de 5 créditos = 80 %, cumple. 2 de 3 = 66,67 % mostrado, no cumple. | Según planificación |
| R2 | Contenidos | Medir la fracción de contenidos de destino que tienen al menos una coincidencia con el origen a partir del umbral de similitud. Contar cada contenido de destino una sola vez. Ponderar por horas si todos los pesos requeridos están disponibles y usar pesos iguales cuando falten. | Con cinco contenidos de igual peso, cuatro coincidencias con similitud de al menos 0,75 dan 80 % de cobertura y cumplen. | Según planificación |
| R3 | Carga horaria | Mostrar horas de origen, horas de destino y su razón si puede calcularse. El borrador del sprint propone mostrar una advertencia si la razón es inferior a 0,80 y continuar la evaluación. | 60 de 80 horas = 75 %. Con el criterio propuesto, se muestra una advertencia. | Por validar |
| R4 | Resultado combinado | Filtrar por aprobación de origen según R8 y aplicar la excepción R7 cuando corresponda. Si R1 falla con datos suficientes, emitir Rechazo por créditos. Si falta información requerida, emitir Datos incompletos. Si R1 cumple y R2 falla, emitir Rechazo por contenidos. Si ambos cumplen con datos suficientes, emitir Homologable. | 85 % de créditos y 79 % de cobertura, con los demás datos requeridos disponibles, dan Rechazo por contenidos. | Según planificación |
| R5 | Es recomendable homologar | Ante un Rechazo por créditos, el profesor puede registrar la acotación Es recomendable homologar con una justificación. Conservar el resultado calculado y mostrar ambos en el informe. | El profesor registra una recomendación justificada para un caso de 2 créditos frente a 3. El resultado calculado sigue siendo Rechazo por créditos. | Según planificación |
| R6 | Homologación compuesta | Sumar los créditos y las horas de las asignaturas de origen distintas y aprobadas. Evaluar la cobertura sobre la unión de sus contenidos. El agente propone grupos de dos y el profesor puede agrupar manualmente más de dos. | Dos materias de 2 créditos y 32 horas cada una suman 4 créditos y 64 horas. Si las dos cubren el mismo tema de destino, ese tema aporta una vez. | Según planificación |
| R7 | Transversales | Emitir Transversal directa si origen y destino son transversales institucionales y el origen está aprobado. Mostrar R1 como dato informativo y omitir R2. | Un origen transversal aprobado de 2 créditos y un destino transversal de 3 créditos dan Transversal directa. El 66,67 % de créditos se muestra como referencia. | Según planificación |
| R8 | Origen aprobado | Considerar solo asignaturas de origen aprobadas. Comparar la nota con un mínimo configurable e inclusivo una vez que el docente confirme el valor y su aplicación según el origen. | Si el docente fijara 7 sobre 10, una nota de 7,00 pasaría el filtro y una de 6,99 quedaría excluida. | Por validar |
| R9 | Uso único del origen | Una asignatura cursada puede respaldar como máximo un destino en el expediente. Si hay conflicto entre propuestas, mostrar una advertencia y destacar la mejor según el criterio acordado. El profesor decide la asignación final. | Una materia cursada aparece en propuestas de dos destinos. Solo una equivalencia aceptada puede usar ese origen. | Por validar |
| R10 | Consistencia con el histórico | Mostrar la decisión previa y su justificación cuando exista un antecedente para el mismo par origen–destino. Calcular el resultado actual con las reglas y los datos de la ejecución. | Un par previamente homologado conserva su antecedente. Si hoy obtiene 78 % de cobertura con créditos suficientes, el resultado actual es Rechazo por contenidos. | Según planificación |
| R11 | Estudiantes externos | Aplicar R1 a R9 con los contenidos de la hoja Contenidos del XLSX, asociados por código de asignatura. Identificar la procedencia como contenidos declarados en el sílabo. El criterio propuesto omite el catálogo de origen y los precedentes. Si R2 requiere temas y faltan, aplicar R4. | Una materia externa con créditos suficientes y cuatro de cinco contenidos de destino cubiertos alcanza 80 % con pesos iguales. Si carece de temas y requiere R2, queda como Datos incompletos. | Por validar |

## Criterios de implementación

- Comparar los valores antes de redondear. Una razón de créditos de 0,799 queda por debajo de 0,80. Una similitud de 0,75 alcanza el umbral inicial de coincidencia. El umbral de similitud se calibra en T13.6.
- Calcular las razones con un denominador positivo y valores de origen disponibles. Registrar datos ausentes, inválidos y reglas omitidas. Con datos de créditos suficientes, el rechazo de R1 tiene prioridad sobre la falta de contenidos en R4. R7 es la excepción transversal.
- Distinguir horas totales de asignatura y horas de ponderación de contenidos. Ante ausencia de horas de ponderación, R2 usa pesos iguales. Como criterio propuesto para R3 y R4, si faltan horas totales, registrar **Datos incompletos** cuando R1 no haya determinado ya un rechazo y R7 no aplique.
- Para evitar la doble ponderación, se propone usar los subtemas como elementos de comparación cuando existan. Las unidades sin subtemas aportan el tema. La evidencia conserva la unidad de cada coincidencia.
- La nota mínima se deja en `null` hasta la validación docente. La escala de entrada actual es de 0 a 10.

## Validación con el docente

| Regla | Decisión que debe confirmarse | Acuerdo |
| --- | --- | --- |
| R3 y R4 | ¿Menos del 80 % de horas genera una advertencia o impide homologar? ¿La falta de horas totales deja el caso como Datos incompletos? | Pendiente |
| R8 | ¿Cuál es la nota mínima sobre 10? ¿Aplica por igual a internos y externos? ¿Qué conversión o evidencia sirve cuando el historial externo usa otra escala? | Pendiente |
| R11 | ¿Basta el contenido declarado en el XLSX o se exige un sílabo verificable? ¿Qué acredita una transversal externa? ¿Se confirma omitir los precedentes para externos? | Pendiente |
| R9 | ¿Cómo se prioriza una propuesta cuando el mismo origen compite por dos destinos? ¿Cómo se resuelve un empate? | Pendiente |

**Docente, fecha y evidencia de revisión** · Pendientes de registro.

## Dependencias de implementación

- **R7.** Agregar `EsTransversal` al catálogo académico y definir cómo acreditarlo para orígenes externos.
- **R11.** La importación valida la hoja `Contenidos` y actualmente guarda el historial sin esas filas. Para evaluar cobertura externa hay que persistir y consultar temas y subtemas por asignatura cursada. [Contrato de importación](../api/plantilla-historial-externo.md)
