// Ejecuta el código real de la función con Auth/BD simulados; no envía solicitudes externas.
import { readFileSync } from 'node:fs';
import { stripTypeScriptTypes } from 'node:module';
import { runInNewContext } from 'node:vm';
import assert from 'node:assert/strict';

const fuente = readFileSync(new URL('../../BaseDatos/Funciones/admin-users/index.ts', import.meta.url), 'utf8')
  .replace(/^import .*?;\r?\n/, '');
const codigo = stripTypeScriptTypes(fuente);
const tienda = '20000000-0000-0000-0000-000000000002';
let total = 0;

async function ejecutar(cuerpo, opciones = {}) {
  let manejador;
  const llamadas = { cambio: 0, marca: 0, creada: null };
  const usuario = { id: 'actor', is_anonymous: false, app_metadata: { proveedor: 'email',
    cogana_admin_cambio_contrasena_pendiente: opciones.pendiente ?? false },
    user_metadata: { cogana_admin_cambio_contrasena_pendiente: true } };
  const cliente = {
    auth: {
      getUser: async () => ({ data: { user: opciones.sinSesion ? null : usuario }, error: null }),
      admin: {
        listUsers: async () => ({ data: { users: opciones.existente ? [{ id: 'existente', email: 'nuevo@example.test' }] : [] } }),
        createUser: async datos => { llamadas.creada = datos; return { data: { user: { id: 'nuevo' } } }; },
        updateUserById: async (id, datos) => { llamadas.marca++; assert.equal(id, 'actor');
          assert.equal(datos.app_metadata.proveedor, 'email');
          assert.equal(datos.app_metadata.cogana_admin_cambio_contrasena_pendiente, false);
          return { error: opciones.falloMarca ? new Error('error simulado') : null }; }
      }
    },
    from: tabla => {
      const consulta = { select: () => consulta, eq: () => consulta,
        maybeSingle: async () => ({ data: tabla === 'store_memberships' && cuerpo.action === 'invite' && llamadas.creada
          ? null : { role: opciones.rol ?? 'admin', is_active: !opciones.suspendido } }),
        upsert: async () => ({ error: null }), insert: async () => ({ error: null }) };
      // En invitación, la primera consulta es del actor y la segunda de la cuenta objetivo.
      if (tabla === 'store_memberships') {
        let objetivo = false;
        consulta.eq = (clave, valor) => { if (clave === 'user_id' && valor !== 'actor') objetivo = true; return consulta; };
        consulta.maybeSingle = async () => ({ data: objetivo ? null : { role: opciones.rol ?? 'admin', is_active: !opciones.suspendido } });
      }
      return consulta;
    }
  };
  runInNewContext(codigo, {
    createClient: () => cliente, Response, Request, console,
    Deno: { env: { get: clave => clave === 'SUPABASE_URL' ? 'https://proyecto.supabase.co' : 'clave-simulada' }, serve: fn => manejador = fn },
    fetch: async (url, solicitud) => {
      llamadas.cambio++;
      assert.equal(url, 'https://proyecto.supabase.co/auth/v1/user');
      assert.equal(solicitud.headers.Authorization, 'Bearer token-simulado');
      return new Response('{}', { status: opciones.falloAuth ? 422 : 200 });
    }
  });
  const respuesta = await manejador(new Request('https://funcion.test', { method: 'POST',
    headers: { Authorization: 'Bearer token-simulado' }, body: JSON.stringify({ store_id: tienda, ...cuerpo }) }));
  return { estado: respuesta.status, llamadas };
}

async function probar(nombre, cuerpo, opciones, esperado, comprobar = () => {}) {
  const resultado = await ejecutar(cuerpo, opciones);
  assert.equal(resultado.estado, esperado, nombre);
  comprobar(resultado.llamadas);
  console.log(`Correcto: ${nombre}`);
  total++;
}
const cambio = { action: 'change-password', new_password: 'NuevaSegura1!' };
await probar('Rechaza sesión inválida', cambio, { sinSesion: true }, 401);
await probar('Rechaza membresía suspendida', cambio, { pendiente: true, suspendido: true }, 403);
await probar('Rechaza repartidor en flujo del escritorio', cambio, { pendiente: true, rol: 'driver' }, 403);
await probar('user_metadata no autoriza retirar la marca', cambio, {}, 409, x => assert.equal(x.cambio, 0));
await probar('Bloquea operaciones normales con contraseña temporal', { action: 'list' }, { pendiente: true }, 403);
await probar('Rechaza contraseña corta', { ...cambio, new_password: 'corta' }, { pendiente: true }, 400);
await probar('Auth rechazado conserva marca', cambio, { pendiente: true, falloAuth: true }, 400,
  x => { assert.equal(x.cambio, 1); assert.equal(x.marca, 0); });
await probar('Fallo al retirar marca conserva bloqueo', cambio, { pendiente: true, falloMarca: true }, 503);
await probar('Administrador cambia solo su contraseña; conserva otros metadatos', cambio, { pendiente: true }, 200,
  x => { assert.equal(x.cambio, 1); assert.equal(x.marca, 1); });
const invitacion = { action: 'invite', email: 'nuevo@example.test', full_name: 'Cuenta de prueba', role: 'admin', temporary_password: 'Temporal1!Segura' };
await probar('Solo propietario invita', invitacion, {}, 403);
await probar('Cuenta nueva se marca desde servidor', invitacion, { rol: 'owner' }, 200,
  x => assert.equal(x.creada.app_metadata.cogana_admin_cambio_contrasena_pendiente, true));
await probar('Cuenta existente conserva contraseña y metadatos', invitacion, { rol: 'owner', existente: true }, 200,
  x => { assert.equal(x.creada, null); assert.equal(x.marca, 0); assert.equal(x.cambio, 0); });
console.log(`${total} comprobaciones correctas; sin conexión a producción.`);
