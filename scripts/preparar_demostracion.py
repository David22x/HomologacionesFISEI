"""Datos sintéticos de homologación. Sin --aplicar solo consulta y muestra cantidades.
--retirar-actuales usa la eliminación lógica del API, nunca borra expedientes o auditoría.
El respaldo y el progreso se guardan en obj/demostracion (excluido de Git).
"""
import argparse
import json
import secrets
import sys
sys.dont_write_bytecode = True
from verificar_flujo import ApiError, Client, ROOT, config

TEMAS = [
    ('Algoritmos y estructuras de datos', 'Arreglos, listas enlazadas y complejidad algorítmica'),
    ('Bases de datos relacionales', 'Modelo relacional, claves primarias y consultas SQL'),
    ('Redes de computadoras', 'Protocolos TCP/IP, direccionamiento IP y enrutamiento'),
    ('Pruebas de software', 'Pruebas unitarias, aserciones y diseño de casos de prueba'),
]
OTROS = [('Contabilidad financiera', 'Balance general, activos, pasivos y patrimonio'),
         ('Macroeconomía', 'Inflación, oferta monetaria y política fiscal')]
CASOS = [
    ('directa', 'Ana', 'Directa', ['directa'], False, False,
     'Una asignatura con créditos y contenidos idénticos; se espera homologable.'),
    ('compuesta', 'Bruno', 'Compuesta', ['parte1', 'parte2'], False, False,
     'Dos materias de 2,5 créditos cubren juntas los cuatro temas del destino de 5 créditos.'),
    ('cercana', 'Carla', 'Revision', ['cercana'], False, False,
     'Tres de cuatro temas (75 % previsto): revisar evidencia y justificar la decisión docente.'),
    ('creditos', 'Diego', 'Creditos', ['creditos'], False, False,
     'Dos créditos frente a cinco (40 %): rechazo por créditos, aunque coinciden los contenidos.'),
    ('contenidos', 'Elena', 'Contenidos', ['contenidos'], False, False,
     'Créditos suficientes pero temas de economía: rechazo; no debe recomendar combinaciones sin afinidad.'),
    ('externa', 'Fabian', 'Externo', ['directa'], True, False,
     'Estudiante externo con temas y subtemas completos: permite comparar sin asignatura local asociada.'),
    ('incompleta', 'Gabriela', 'Pendiente', ['directa'], True, True,
     'Estudiante externo sin temas y subtemas: debe pedir completar la información y bloquear el análisis.'),
]


def cedula():
    digits = '180' + str(secrets.randbelow(1000000)).zfill(6)
    values = [int(x) * (2 if i % 2 == 0 else 1) for i, x in enumerate(digits)]
    return digits + str((10 - sum(x - 9 if x > 9 else x for x in values) % 10) % 10)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--aplicar', action='store_true')
    parser.add_argument('--retirar-actuales', action='store_true')
    parser.add_argument('--evaluar', action='store_true')
    parser.add_argument('--analizar-ia', action='store_true')
    parser.add_argument('--base-url', default='http://127.0.0.1:5080')
    args = parser.parse_args()
    api = Client(args.base_url)
    cfg = config(ROOT / 'infrastructure/.env')
    def login(email, key):
        if not cfg.get(key):
            raise ValueError('Falta configuración local: ' + key)
        return api.json('POST', '/auth/login', {'email': email, 'password': cfg[key]})['accessToken']
    admin = login('admin@uta.edu.ec', 'ADMIN_PASSWORD')
    teacher = login('profesor@uta.edu.ec', 'PROFESSOR_PASSWORD')
    students = api.json('GET', '/api/v1/estudiantes', token=admin)
    print(f'Estudiantes activos: {len(students)}. Casos sintéticos previstos: {len(CASOS)}.')
    if not args.aplicar:
        print('Sin cambios. Usa --aplicar; --retirar-actuales retira únicamente el conjunto respaldado antes de la carga.')
        return
    folder = ROOT / 'obj/demostracion'
    folder.mkdir(parents=True, exist_ok=True)
    checkpoint = folder / 'resultado.json'
    if checkpoint.exists():
        state = json.loads(checkpoint.read_text(encoding='utf-8'))
        if state.get('baseUrl') != args.base_url:
            raise ValueError('El respaldo pertenece a otro Gateway; no se reutilizará.')
    else:
        state = {'baseUrl': args.base_url, 'lote': secrets.token_hex(3).upper(), 'creados': {}, 'casos': [], 'retirados': []}
        (folder / 'estudiantes-antes.json').write_text(json.dumps(students, ensure_ascii=False, indent=2), encoding='utf-8')
        state['anteriores'] = [x['id'] for x in students]
    def save():
        checkpoint.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding='utf-8')
    save()
    def post_once(key, path, payload=None, token=admin):
        if key not in state['creados']:
            state['creados'][key] = api.json('POST', path, payload, token)
            save()
        return state['creados'][key]
    tag = state['lote']
    def curriculum(key, name, code):
        career = post_once(key, '/api/v1/catalogo/carreras', {'codigo': code + tag, 'nombre': name, 'esDemo': True})
        malla = post_once(key + '_malla', f"/api/v1/catalogo/carreras/{career['id']}/mallas", {'version': 'DEMO-' + tag})
        level = post_once(key + '_nivel', f"/api/v1/catalogo/mallas/{malla['id']}/niveles", {'numero': 1})
        return career, malla, level
    origin, om, ol = curriculum('origen', 'DEMO Software - origen', 'DSW')
    target, tm, tl = curriculum('destino', 'DEMO TI - destino', 'DTI')
    def subject(key, malla, level, name, credits, topics):
        s = post_once(key, f"/api/v1/catalogo/mallas/{malla['id']}/asignaturas", {
            'nivelId': level['id'], 'codigo': 'DEMO-' + key.upper(), 'nombre': name,
            'creditos': credits, 'horas': round(credits * 20), 'unidadOrganizacional': 'Profesional', 'esTransversal': False})
        for i, (title, detail) in enumerate(topics, 1):
            topic = post_once(key + '_tema' + str(i), f"/api/v1/catalogo/asignaturas/{s['id']}/temas",
                              {'nombre': title, 'orden': i, 'horasEstimadas': 25})
            post_once(key + '_sub' + str(i), f"/api/v1/catalogo/temas/{topic['id']}/subtemas",
                      {'nombre': detail, 'orden': 1, 'horasEstimadas': 25})
        return s
    dest = subject('destino_materia', tm, tl, 'FUNDAMENTOS DE COMPUTACION', 5, TEMAS)
    specifications = {
        'directa': ('FUNDAMENTOS DE COMPUTACION', 5, TEMAS),
        'parte1': ('FUNDAMENTOS DE COMPUTACION I', 2.5, TEMAS[:2]),
        'parte2': ('FUNDAMENTOS DE COMPUTACION II', 2.5, TEMAS[2:]),
        'cercana': ('FUNDAMENTOS DE COMPUTACION - PARCIAL', 5, TEMAS[:3]),
        'creditos': ('FUNDAMENTOS DE COMPUTACION - CREDITOS', 2, TEMAS),
        'contenidos': ('ECONOMIA Y CONTABILIDAD', 5, OTROS),
    }
    sources = {key: subject(key, om, ol, *spec) for key, spec in specifications.items()}
    for key, malla in [('origen_publicada', om), ('destino_publicada', tm)]:
        post_once(key, f"/api/v1/catalogo/mallas/{malla['id']}/publicar")
    for case, name, surname, keys, external, missing, expected in CASOS:
        courses = []
        for key in keys:
            s = sources[key]
            courses.append({'asignaturaId': None if external else s['id'], 'codigo': s['codigo'], 'nombre': s['nombre'],
                'creditos': s['creditos'], 'horas': s['horas'], 'nota': 10, 'periodo': '2026-A',
                'temas': [] if missing else [{'nombre': title, 'subtemas': [detail], 'horas': 25}
                                             for title, detail in specifications[key][2]]})
        student = post_once('estudiante_' + case, '/api/v1/estudiantes', {
            'cedula': 'DEMO' + tag + case.upper()[:8] if external else cedula(),
            'tipoIdentificacion': 1 if external else 0, 'nombres': name, 'apellidos': 'DEMO ' + surname,
            'tipo': 1 if external else 0, 'historial': {
                'carreraOrigenId': None if external else origin['id'], 'mallaOrigenId': None if external else om['id'],
                'carreraOrigenNombre': 'Computacion (universidad externa DEMO)' if external else None,
                'institucionOrigen': 'Universidad de demostracion - datos ficticios' if external else None,
                'nivelActual': 1, 'asignaturas': courses}}, teacher)
        exp = post_once('expediente_' + case, '/api/v1/homologacion/expedientes', {
            'estudianteRefId': student['id'], 'carreraDestinoRefId': target['id'], 'mallaAplicableRefId': tm['id']}, teacher)
        existing = next((x for x in state['casos'] if x['caso'] == case), None)
        if not existing:
            existing = {'caso': case, 'nombre': name + ' DEMO ' + surname, 'estudianteId': student['id'],
                        'expedienteId': None, 'esperado': expected}
            state['casos'].append(existing)
        existing['expedienteId'] = exp['id']
        path = '/api/v1/homologacion/expedientes/' + exp['id']
        if args.evaluar and 'evaluacion' not in existing:
            saved = api.json('GET', '/api/v1/estudiantes/' + student['id'], token=teacher)
            pairs = [{'asignaturaCursadaId': c['id'], 'asignaturaDestinoId': dest['id']} for c in saved['historial']['asignaturas']]
            api.json('PUT', path + '/pares', pairs, teacher)
            try:
                if missing:
                    api.json('POST', path + '/analizar-ia', token=teacher)
                    raise AssertionError('El historial incompleto permitió analizar.')
                result = api.json('POST', path + '/evaluar-reglas', token=teacher)
                existing['evaluacion'] = result
            except ApiError as error:
                if missing and error.status == 422:
                    existing['evaluacion'] = {'bloqueadoPorInformacionFaltante': True}
                else:
                    existing['evaluacionPendiente'] = str(error)
            save()
        if args.analizar_ia and not missing and 'analisisIa' not in existing:
            try:
                existing['analisisIa'] = api.json('POST', path + '/analizar-ia', token=teacher)
                existing.pop('analisisPendiente', None)
            except ApiError as error:
                existing['analisisPendiente'] = str(error)
            save()
        print('Preparado: ' + existing['nombre'])
        save()
    # Los siete estudiantes y sus expedientes deben existir antes de retirar la lista original.
    if args.retirar_actuales:
        for student_id in state['anteriores']:
            if student_id not in state['retirados']:
                api.send('DELETE', '/api/v1/estudiantes/' + student_id, token=admin)
                state['retirados'].append(student_id)
                save()
    active = api.json('GET', '/api/v1/estudiantes', token=admin)
    active_ids = {x['id'] for x in active}
    assert all(x['estudianteId'] in active_ids for x in state['casos'])
    if args.retirar_actuales:
        assert not active_ids.intersection(state['anteriores'])
    state['completo'] = all(x.get('expedienteId') for x in state['casos'])
    save()
    print(f"Completado: {len(state['casos'])} casos, {len(state['retirados'])} estudiantes anteriores retirados. Activos: {len(active)}.")


if __name__ == '__main__':
    main()
