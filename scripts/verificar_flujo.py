"""Prueba local reproducible. Sin --crear-caso solo consulta servicios.
Con --crear-caso crea datos SINTETICOS identificados como QA y conserva el expediente
resuelto para poder demostrarlo en la interfaz. No borra ni modifica datos previos.
"""
import argparse
import hashlib
import io
import json
import secrets
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from datetime import datetime, timezone
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]

class ApiError(Exception):
    def __init__(self, status, path):
        self.status = status
        super().__init__(f"HTTP {status}: {path}")

class Client:
    def __init__(self, base):
        if urllib.parse.urlsplit(base).hostname not in {"localhost", "127.0.0.1", "::1"}:
            raise ValueError("Esta prueba solo permite un Gateway local.")
        self.base = base.rstrip('/')
        self.http = urllib.request.build_opener(urllib.request.ProxyHandler({}))

    def send(self, method, path, data=None, token=None, raw=None, content_type=None, timeout=230):
        headers = {}
        if token:
            headers['Authorization'] = 'Bearer ' + token
        if data is not None:
            raw = json.dumps(data).encode()
            content_type = 'application/json'
        if content_type:
            headers['Content-Type'] = content_type
        request = urllib.request.Request(self.base + path, data=raw, headers=headers, method=method)
        try:
            with self.http.open(request, timeout=timeout) as response:
                body = response.read()
                result = json.loads(body) if body and 'json' in response.headers.get('Content-Type', '') else body
                return response.status, result, {k.lower(): v for k, v in response.headers.items()}
        except urllib.error.HTTPError as error:
            raise ApiError(error.code, path) from None

    def json(self, method, path, data=None, token=None):
        return self.send(method, path, data, token)[1]

    def upload(self, path, name, content, token, data=None):
        boundary = 'QaBoundary' + secrets.token_hex(8)
        body = (f'--{boundary}\r\nContent-Disposition: form-data; name="archivo"; filename="{name}"\r\n'
                'Content-Type: application/octet-stream\r\n\r\n').encode() + content + b'\r\n'
        if data is not None:
            body += (f'--{boundary}\r\nContent-Disposition: form-data; name="datos"\r\n\r\n').encode() + json.dumps(data).encode() + b'\r\n'
        body += f'--{boundary}--\r\n'.encode()
        return self.send('POST', path, token=token, raw=body, content_type='multipart/form-data; boundary=' + boundary)[1]

def config(path):
    result = {}
    for line in Path(path).read_text(encoding='utf-8-sig').splitlines():
        if line.strip() and not line.lstrip().startswith('#') and '=' in line:
            key, value = line.split('=', 1)
            result[key.strip()] = value.strip().strip('"').strip("'")
    return result

def pdf(lines):
    def escape(value):
        return value.replace('\\', '\\\\').replace('(', '\\(').replace(')', '\\)')
    stream = '\n'.join(f'BT /F1 10 Tf 1 0 0 1 35 {790-i*22} Tm ({escape(line)}) Tj ET' for i, line in enumerate(lines))
    objects = ['<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
        '<< /Type /Pages /Count 1 /Kids [5 0 R] >>', '<< /Type /Catalog /Pages 2 0 R >>',
        f'<< /Length {len(stream)} >>\nstream\n{stream}\nendstream',
        '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 1 0 R >> >> /Contents 4 0 R >>']
    output = '%PDF-1.4\n'
    offsets = []
    for index, obj in enumerate(objects, 1):
        offsets.append(len(output))
        output += f'{index} 0 obj\n{obj}\nendobj\n'
    xref = len(output)
    output += 'xref\n0 6\n0000000000 65535 f \n' + ''.join(f'{offset:010} 00000 n \n' for offset in offsets)
    output += f'trailer\n<< /Size 6 /Root 3 0 R >>\nstartxref\n{xref}\n%%EOF'
    return output.encode('ascii')

def xlsx(template, identifier):
    ns = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
    rows = [[identifier, 'Pasaporte', 'VALIDACION', 'EXTERNA', 'Universidad QA', 'Computacion QA', 1],
            ['EXT1', 'PROGRAMACION DE PRUEBA', 3, 48, 10, '2026-A'],
            ['EXT1', 'Variables y tipos de datos', 'Declaracion de variables']]
    destination = io.BytesIO()
    with zipfile.ZipFile(io.BytesIO(template)) as source, zipfile.ZipFile(destination, 'w', zipfile.ZIP_DEFLATED) as target:
        for item in source.infolist():
            content = source.read(item.filename)
            for i, values in enumerate(rows, 1):
                if item.filename == f'xl/worksheets/sheet{i}.xml':
                    tree = ET.fromstring(content)
                    row = ET.SubElement(tree.find(f'{{{ns}}}sheetData'), f'{{{ns}}}row', {'r': '2'})
                    for col, value in enumerate(values):
                        attrs = {'r': chr(65 + col) + '2'}
                        cell = ET.SubElement(row, f'{{{ns}}}c', attrs)
                        if isinstance(value, (int, float)):
                            ET.SubElement(cell, f'{{{ns}}}v').text = str(value)
                        else:
                            cell.set('t', 'inlineStr')
                            ET.SubElement(ET.SubElement(cell, f'{{{ns}}}is'), f'{{{ns}}}t').text = value
                    content = ET.tostring(tree, encoding='utf-8', xml_declaration=True)
            target.writestr(item, content)
    return destination.getvalue()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base-url', default='http://127.0.0.1:5080')
    parser.add_argument('--env-file', default=str(ROOT / 'infrastructure/.env'))
    parser.add_argument('--crear-caso', action='store_true', help='Crea un caso QA nuevo y lo resuelve, con PDF. Conserva sus datos.')
    parser.add_argument('--probar-ia-documentos', action='store_true', help='Además prueba extracción generativa real de un PDF sintético.')
    args = parser.parse_args()
    api = Client(args.base_url)
    cfg = config(args.env_file)
    report = {'inicio': datetime.now(timezone.utc).isoformat(), 'etapas': [], 'creados': {}}
    report_path = ROOT / 'obj/verificacion-flujo/resultado.json'
    report_path.parent.mkdir(parents=True, exist_ok=True)

    def save():
        report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')

    def ok(stage):
        report['etapas'].append(stage)
        save()
        print('OK: ' + stage, flush=True)

    def remember(name, value):
        report['creados'][name] = value['id']
        save()
        return value

    def login(role):
        password = cfg.get(role.upper() + '_PASSWORD')
        if not password:
            raise ValueError('Falta la contraseña de desarrollo del rol ' + role)
        email = 'profesor@uta.edu.ec' if role == 'professor' else 'admin@uta.edu.ec'
        return api.json('POST', '/auth/login', {'email': email, 'password': password})['accessToken']

    try:
        teacher = login('professor')
        ok('Login de profesor')
        for title, path in [('Catalogo', '/api/v1/catalogo/carreras'), ('Estudiantes', '/api/v1/estudiantes'), ('Expedientes', '/api/v1/homologacion/expedientes')]:
            assert isinstance(api.json('GET', path, token=teacher), list)
            ok(title + ' por Gateway')
        def probar_extraccion_ia(document):
            extracted = api.upload('/api/v1/documentos/records/analizar-registro?externo=true', 'qa-externo.pdf', document, teacher)
            assert extracted['filas'] and extracted['filas'][0]['nota'] == 10, 'La extraccion generativa devolvio respaldo manual o datos pendientes'
            report['extraccionGenerativaCorrecta'] = True
            ok('Extraccion generativa de PDF con Ollama real')

        if not args.crear_caso:
            if args.probar_ia_documentos:
                probar_extraccion_ia(pdf(['University QA', 'Student: VALIDACION PRUEBA', 'Passport: QA12345', 'Career: Computing',
                    'Term: 2026-A', 'Grade scale: 10', 'Programming 10 Passed']))
            report['correcto'] = True
            save()
            print('Consultas completas. Para verificar registro, decisiones y PDF usa --crear-caso.')
            return

        admin = login('admin')
        suffix = secrets.token_hex(3).upper()
        report['caso'] = 'QA-' + suffix
        save()
        def career(label, code):
            c = remember('carrera_' + code, api.json('POST', '/api/v1/catalogo/carreras', {'codigo': code + suffix, 'nombre': 'QA ' + label + ' ' + suffix, 'esDemo': True}, admin))
            m = remember('malla_' + code, api.json('POST', f"/api/v1/catalogo/carreras/{c['id']}/mallas", {'version': 'QA-' + suffix}, admin))
            level = api.json('POST', f"/api/v1/catalogo/mallas/{m['id']}/niveles", {'numero': 1}, admin)
            subject = remember('asignatura_' + code, api.json('POST', f"/api/v1/catalogo/mallas/{m['id']}/asignaturas", {'nivelId': level['id'], 'codigo': code + '-PROG', 'nombre': 'PROGRAMACION DE PRUEBA', 'creditos': 3, 'horas': 48, 'unidadOrganizacional': 'Basica'}, admin))
            topic = api.json('POST', f"/api/v1/catalogo/asignaturas/{subject['id']}/temas", {'nombre': 'Variables y tipos de datos', 'orden': 1, 'horasEstimadas': 48}, admin)
            api.json('POST', f"/api/v1/catalogo/temas/{topic['id']}/subtemas", {'nombre': 'Declaracion de variables y expresiones', 'orden': 1, 'horasEstimadas': 48}, admin)
            api.json('POST', f"/api/v1/catalogo/mallas/{m['id']}/publicar", token=admin)
            return c, m, subject
        origin, origin_malla, origin_subject = career('Origen', 'QO')
        target, target_malla, target_subject = career('Destino', 'QD')
        ok('Catalogo QA independiente con mallas publicadas y contenidos')
        # Identificación sintética válida y nueva: nunca reutiliza a un estudiante previo.
        while True:
            digits = '180' + str(secrets.randbelow(1000000)).zfill(6)
            total = sum((int(x) * 2 - 9 if int(x) * 2 > 9 else int(x) * 2) if i % 2 == 0 else int(x) for i, x in enumerate(digits))
            identification = digits + str((10 - total % 10) % 10)
            try:
                api.json('GET', '/api/v1/estudiantes/cedula/' + identification, token=teacher)
            except ApiError as error:
                if error.status == 404:
                    break
                raise
        document = pdf(['Universidad Tecnica de Ambato', 'CARRERA DE ' + origin['nombre'],
            'CERTIFICA: Que el/la (Sr(a),Srta).', 'VALIDACION PRUEBA TECNICA con documento de identidad numero: ' + identification,
            'periodo AGO/25-ENE/26', 'ASIGNATURA PROMEDIO ESTADO', 'PROGRAMACION DE PRUEBA 10.0/10 APROBADO'])
        preview = api.upload('/api/v1/documentos/records/analizar-registro', 'qa-record.pdf', document, teacher)
        assert len(preview['filas']) == 1 and preview['filas'][0]['nota'] == 10 and preview['perfil']['nombres']
        preview['filas'][0].update(asignaturaId=origin_subject['id'], creditos=3, horas=48)
        registration = {'documento': preview, 'tipo': 0, 'carreraOrigenId': origin['id'], 'mallaOrigenId': origin_malla['id'], 'nivelActual': 1}
        student = remember('estudiante', api.upload('/api/v1/documentos/records/registrar', 'qa-record.pdf', document, teacher, registration))
        api.upload('/api/v1/documentos/records/registrar', 'qa-record.pdf', document, teacher, registration)
        saved = api.json('GET', f"/api/v1/estudiantes/{student['id']}", token=teacher)
        assert len(saved['historial']['asignaturas']) == 1
        ok('Registro desde PDF con identidad, notas e historial; reintento sin duplicados')

        template = api.send('GET', '/api/v1/documentos/importaciones/plantilla', token=teacher)[1]
        external_file = xlsx(template, 'QAX' + suffix)
        validated = api.upload('/api/v1/documentos/importaciones', 'qa-externo.xlsx', external_file, teacher)
        assert not validated['errores'] and validated['vistaPrevia']['historial']['asignaturas'][0]['temas'][0]['subtemas']
        external = remember('estudiante_externo', api.upload('/api/v1/documentos/importaciones/confirmar', 'qa-externo.xlsx', external_file, teacher, validated['vistaPrevia']))
        assert external['listoParaHomologar']
        ok('XLSX externo conserva temas y subtemas y confirma el registro')

        exp = remember('expediente', api.json('POST', '/api/v1/homologacion/expedientes', {'estudianteRefId': student['id'], 'carreraDestinoRefId': target['id'], 'mallaAplicableRefId': target_malla['id']}, teacher))
        path = '/api/v1/homologacion/expedientes/' + exp['id']
        pairs = api.json('POST', path + '/pares/precargar', token=teacher)
        assert len(pairs) == 1
        evaluation = api.json('POST', path + '/evaluar-reglas', token=teacher)
        assert evaluation['resultados'][0]['evaluacion']['reglas']['resultado'] == 0
        analysis = api.json('POST', path + '/analizar-ia', token=teacher)
        assert analysis['propuestas'][0]['evaluacion']['reglas']['resultado'] == 0
        retry = api.json('POST', path + '/analizar-ia', token=teacher)
        assert retry['id'] == analysis['id']
        proposal = analysis['propuestas'][0]
        ids = [x['asignaturaCursadaId'] for x in proposal['origenes']]
        api.json('PUT', path + '/seleccion', [{'asignaturaDestinoId': target_subject['id'], 'origenIds': ids}], teacher)
        api.json('PUT', path + '/analisis-ia/propuestas/revision', {'asignaturaDestinoId': target_subject['id'], 'origenIds': ids, 'aceptarPropuestaIa': True, 'observacion': 'Validacion tecnica con datos sinteticos y contenidos identicos.'}, teacher)
        resolution = api.json('POST', path + '/resolver', token=teacher)
        assert resolution['estado'] == 'Resuelto' and resolution['soloLectura']
        ok('Reglas, Ollama real, seleccion sincronizada, decision docente y resolucion')
        try:
            api.json('POST', path + '/evaluar-reglas', token=teacher)
            raise AssertionError('El expediente resuelto permitio una modificacion')
        except ApiError as error:
            assert error.status == 409
        audit = api.json('GET', path + '/auditoria', token=teacher)
        assert len(audit) >= 4
        ok('Bloqueo de solo lectura y eventos de auditoria')
        report_path_api = '/api/v1/informes/expedientes/' + exp['id']
        first = api.json('POST', report_path_api + '/generar', token=teacher)
        second = api.json('POST', report_path_api + '/generar', token=teacher)
        assert first['version'] == second['version']
        status, content, headers = api.send('GET', report_path_api + '/' + str(first['version']) + '/pdf', token=teacher)
        assert status == 200 and content.startswith(b'%PDF-')
        assert hashlib.sha256(content).hexdigest().upper() == first['huellaSha256'] == headers['x-content-sha256']
        report.update(informeVersion=first['version'], huellaSha256=first['huellaSha256'])
        ok('Generacion y descarga de PDF real con huella verificada y version idempotente')
        report['flujoPrincipalCorrecto'] = True
        save()
        if args.probar_ia_documentos:
            probar_extraccion_ia(document)
        report['correcto'] = True
        save()
        print('Caso terminado: ' + report['caso'] + '. Expediente QA resuelto: ' + exp['id'])
    except Exception as error:
        report['correcto'] = False
        report['error'] = str(error)
        save()
        raise

if __name__ == '__main__':
    main()
