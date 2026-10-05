import { createClient, type User } from "@supabase/supabase-js";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, apikey, content-type",
};

type RequestBody = {
  action?: "list" | "invite" | "update" | "change-password";
  store_id?: string;
  user_id?: string;
  email?: string;
  full_name?: string;
  phone?: string | null;
  role?: "owner" | "admin" | "driver";
  is_active?: boolean;
  temporary_password?: string;
  new_password?: string;
};

const roles = new Set(["owner", "admin", "driver"]);

function respond(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { ...corsHeaders, "Content-Type": "application/json; charset=utf-8" },
  });
}

async function listAllUsers(admin: ReturnType<typeof createClient>) {
  const result: User[] = [];
  let page = 1;

  while (true) {
    const { data, error } = await admin.auth.admin.listUsers({ page, perPage: 1000 });
    if (error) throw error;
    result.push(...data.users);
    if (data.users.length < 1000) return result;
    page += 1;
  }
}

Deno.serve(async (request: Request) => {
  if (request.method === "OPTIONS") return new Response("ok", { headers: corsHeaders });
  if (request.method !== "POST") return respond({ error: "Método no permitido." }, 405);

  try {
    const url = Deno.env.get("SUPABASE_URL");
    const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
    const authorization = request.headers.get("Authorization");
    if (!url || !serviceRoleKey) return respond({ error: "Servicio sin configurar." }, 500);
    if (!authorization?.startsWith("Bearer ")) return respond({ error: "Sesión requerida." }, 401);

    const token = authorization.slice("Bearer ".length);
    const admin = createClient(url, serviceRoleKey, {
      auth: { persistSession: false, autoRefreshToken: false },
    });

    const { data: authData, error: authError } = await admin.auth.getUser(token);
    if (authError || !authData.user || authData.user.is_anonymous) {
      return respond({ error: "La sesión no es válida." }, 401);
    }

    const body = await request.json() as RequestBody;
    if (!body.store_id || !body.action) {
      return respond({ error: "Faltan datos de la operación." }, 400);
    }

    const { data: actorMembership, error: actorError } = await admin
      .from("store_memberships")
      .select("role,is_active")
      .eq("store_id", body.store_id)
      .eq("user_id", authData.user.id)
      .maybeSingle();

    if (actorError) throw actorError;
    if (!actorMembership?.is_active || !["owner", "admin"].includes(actorMembership.role)) {
      return respond({ error: "Esta cuenta no tiene acceso al directorio de personal." }, 403);
    }
    // Contrato de Auth del escritorio: solo el servidor puede retirar esta marca.
    const cambioPendiente = authData.user.app_metadata?.cogana_admin_cambio_contrasena_pendiente === true;
    if (body.action === "change-password") {
      if (!cambioPendiente) return respond({ error: "No hay un cambio inicial pendiente." }, 409);
      if (typeof body.new_password !== "string" || body.new_password.trim().length === 0 || body.new_password.length < 10) {
        return respond({ error: "La contraseña debe tener al menos 10 caracteres." }, 400);
      }
      // Usa Auth con el JWT del propio usuario para respetar sus reglas de contraseña y reautenticación.
      const clavePublica = Deno.env.get("SUPABASE_ANON_KEY");
      if (!clavePublica) return respond({ error: "Servicio sin configurar." }, 500);
      const respuesta = await fetch(`${url}/auth/v1/user`, {
        method: "PUT",
        headers: { apikey: clavePublica, Authorization: authorization, "Content-Type": "application/json" },
        body: JSON.stringify({ password: body.new_password }),
      });
      if (!respuesta.ok) return respond({ error: "Auth rechazó la nueva contraseña. Usa una contraseña diferente y segura o verifica de nuevo tu acceso." }, 400);
      // Si falla retirar la marca, se conserva el bloqueo y nunca se habilita el panel por error.
      const { error: marcaError } = await admin.auth.admin.updateUserById(authData.user.id, {
        app_metadata: { ...authData.user.app_metadata, cogana_admin_cambio_contrasena_pendiente: false },
      });
      if (marcaError) return respond({ error: "La contraseña cambió, pero el acceso sigue pendiente. Inicia sesión nuevamente para completar el cambio." }, 503);
      return respond({ message: "Contraseña inicial actualizada." });
    }
    if (cambioPendiente) return respond({ error: "Cambia tu contraseña inicial antes de administrar personal." }, 403);
    if (body.action !== "list" && actorMembership.role !== "owner") {
      return respond({ error: "Solo un propietario activo puede administrar accesos." }, 403);
    }

    if (body.action === "list") {
      const { data: memberships, error: membershipsError } = await admin
        .from("store_memberships")
        .select("user_id,role,is_active,created_at")
        .eq("store_id", body.store_id)
        .order("created_at", { ascending: true });
      if (membershipsError) throw membershipsError;

      const ids = (memberships ?? []).map((membership) => membership.user_id);
      let profiles: Array<{ id: string; full_name: string | null; phone: string | null }> = [];
      if (ids.length > 0) {
        const { data: profileRows, error: profileError } = await admin
          .from("profiles")
          .select("id,full_name,phone")
          .in("id", ids);
        if (profileError) throw profileError;
        profiles = profileRows ?? [];
      }
      const authUsers = await listAllUsers(admin);
      const profileById = new Map(profiles.map((profile) => [profile.id, profile]));
      const authById = new Map(authUsers.map((user) => [user.id, user]));

      const users = (memberships ?? []).map((membership) => {
        const profile = profileById.get(membership.user_id);
        const authUser = authById.get(membership.user_id);
        return {
          id: membership.user_id,
          email: authUser?.email ?? "",
          full_name: profile?.full_name ?? authUser?.user_metadata?.full_name ?? "",
          phone: profile?.phone ?? authUser?.phone ?? null,
          role: membership.role,
          is_active: membership.is_active,
          created_at: membership.created_at,
          last_sign_in_at: authUser?.last_sign_in_at ?? null,
        };
      });

      return respond({ users });
    }

    if (!body.full_name?.trim() || !body.role || !roles.has(body.role)) {
      return respond({ error: "Nombre y rol válidos son obligatorios." }, 400);
    }

    if (body.action === "invite") {
      const email = body.email?.trim().toLowerCase();
      if (!email || !email.includes("@")) {
        return respond({ error: "Ingresa un correo electrónico válido." }, 400);
      }
      if (!body.temporary_password || body.temporary_password.length < 10) {
        return respond({ error: "La contraseña inicial debe tener al menos 10 caracteres." }, 400);
      }

      const authUsers = await listAllUsers(admin);
      let target = authUsers.find((user) => user.email?.toLowerCase() === email);
      const alreadyExisted = Boolean(target);

      if (!target) {
        const { data: created, error: createError } = await admin.auth.admin.createUser({
          email,
          password: body.temporary_password,
          email_confirm: true,
          app_metadata: { cogana_admin_cambio_contrasena_pendiente: true },
          user_metadata: { full_name: body.full_name.trim(), phone: body.phone ?? null },
        });
        if (createError) throw createError;
        target = created.user;
      }

      if (!target) return respond({ error: "Supabase no devolvió el usuario invitado." }, 500);

      const { data: existingMembership, error: existingError } = await admin
        .from("store_memberships")
        .select("role,is_active")
        .eq("store_id", body.store_id)
        .eq("user_id", target.id)
        .maybeSingle();
      if (existingError) throw existingError;
      if (existingMembership?.is_active) {
        return respond({ error: "Este correo ya tiene acceso activo a la tienda." }, 409);
      }

      const { error: profileError } = await admin.from("profiles").upsert({
        id: target.id,
        full_name: body.full_name.trim(),
        phone: body.phone?.trim() || null,
        status: "active",
        updated_at: new Date().toISOString(),
      }, { onConflict: "id" });
      if (profileError) throw profileError;

      const { error: membershipError } = await admin.from("store_memberships").upsert({
        store_id: body.store_id,
        user_id: target.id,
        role: body.role,
        is_active: true,
        updated_at: new Date().toISOString(),
      }, { onConflict: "store_id,user_id" });
      if (membershipError) throw membershipError;

      const { error: auditError } = await admin.from("staff_access_audit").insert({
        store_id: body.store_id,
        actor_user_id: authData.user.id,
        target_user_id: target.id,
        action: alreadyExisted ? "membership_created" : "invite",
        previous_role: existingMembership?.role ?? null,
        new_role: body.role,
        previous_active: existingMembership?.is_active ?? null,
        new_active: true,
      });
      if (auditError) throw auditError;

      return respond({
        message: alreadyExisted
          ? "La cuenta existente fue vinculada a la tienda."
          : "El acceso fue creado correctamente.",
      });
    }

    if (body.action === "update") {
      if (!body.user_id || typeof body.is_active !== "boolean") {
        return respond({ error: "Faltan datos del usuario a actualizar." }, 400);
      }

      const { data: current, error: currentError } = await admin
        .from("store_memberships")
        .select("role,is_active")
        .eq("store_id", body.store_id)
        .eq("user_id", body.user_id)
        .maybeSingle();
      if (currentError) throw currentError;
      if (!current) return respond({ error: "El usuario no pertenece a esta tienda." }, 404);

      if (body.user_id === authData.user.id && (!body.is_active || body.role !== "owner")) {
        return respond({ error: "No puedes suspender ni quitar tu propio rol de propietario." }, 409);
      }

      if (current.role === "owner" && current.is_active &&
          (body.role !== "owner" || !body.is_active)) {
        const { count, error: countError } = await admin
          .from("store_memberships")
          .select("user_id", { count: "exact", head: true })
          .eq("store_id", body.store_id)
          .eq("role", "owner")
          .eq("is_active", true);
        if (countError) throw countError;
        if ((count ?? 0) <= 1) {
          return respond({ error: "La tienda debe conservar al menos un propietario activo." }, 409);
        }
      }

      const { error: profileError } = await admin.from("profiles").upsert({
        id: body.user_id,
        full_name: body.full_name.trim(),
        phone: body.phone?.trim() || null,
        updated_at: new Date().toISOString(),
      }, { onConflict: "id" });
      if (profileError) throw profileError;

      const { error: updateError } = await admin
        .from("store_memberships")
        .update({ role: body.role, is_active: body.is_active, updated_at: new Date().toISOString() })
        .eq("store_id", body.store_id)
        .eq("user_id", body.user_id);
      if (updateError) throw updateError;

      const { error: auditError } = await admin.from("staff_access_audit").insert({
        store_id: body.store_id,
        actor_user_id: authData.user.id,
        target_user_id: body.user_id,
        action: "access_updated",
        previous_role: current.role,
        new_role: body.role,
        previous_active: current.is_active,
        new_active: body.is_active,
      });
      if (auditError) throw auditError;

      return respond({ message: "Acceso actualizado correctamente." });
    }

    return respond({ error: "Operación no reconocida." }, 400);
  } catch (error) {
    console.error("admin-users", error);
    const message = error instanceof Error ? error.message : "Error inesperado.";
    return respond({ error: message }, 500);
  }
});
