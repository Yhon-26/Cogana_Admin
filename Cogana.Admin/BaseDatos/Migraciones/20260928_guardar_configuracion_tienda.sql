create or replace function private.save_store_configuration(
  p_store_id uuid,
  p_store_name text,
  p_address text,
  p_district text,
  p_display_name text,
  p_legal_name text,
  p_tax_id text,
  p_phone text,
  p_whatsapp_phone text,
  p_accepts_pickup boolean,
  p_accepts_delivery boolean,
  p_accepts_cash_pickup boolean,
  p_accepts_yape boolean,
  p_accepts_plin boolean,
  p_accepts_card boolean,
  p_default_preparation_minutes integer,
  p_business_hours jsonb,
  p_schedule_exceptions jsonb,
  p_operation_id uuid
)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $function$
declare
  v_actor uuid := (select auth.uid());
  v_existing private.admin_operation_results%rowtype;
  v_result jsonb;
begin
  if v_actor is null or not private.has_store_role(p_store_id, array['owner','admin']) then
    raise exception using errcode = '42501', message = 'No autorizado para administrar la configuración';
  end if;

  if p_operation_id is null then
    raise exception using errcode = '22023', message = 'operation_id es obligatorio';
  end if;

  perform pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended(p_operation_id::text, 0));

  select * into v_existing
  from private.admin_operation_results
  where store_id = p_store_id and operation_id = p_operation_id;

  if found then
    if v_existing.operation_type <> 'save_store_configuration' then
      raise exception using errcode = '23505', message = 'operation_id ya utilizado';
    end if;
    return v_existing.result || pg_catalog.jsonb_build_object('idempotent_replay', true);
  end if;

  perform 1 from public.stores where id = p_store_id for update;
  if not found then
    raise exception using errcode = '22023', message = 'La tienda no existe';
  end if;

  if pg_catalog.btrim(coalesce(p_store_name, '')) = '' or
     pg_catalog.btrim(coalesce(p_display_name, '')) = '' then
    raise exception using errcode = '22023', message = 'El nombre de la tienda y el nombre comercial son obligatorios';
  end if;
  if pg_catalog.length(pg_catalog.btrim(p_store_name)) > 120 or
     pg_catalog.length(pg_catalog.btrim(p_display_name)) > 120 then
    raise exception using errcode = '22023', message = 'Los nombres no pueden superar 120 caracteres';
  end if;
  if nullif(pg_catalog.btrim(coalesce(p_tax_id, '')), '') is not null and
     pg_catalog.btrim(p_tax_id) !~ '^[0-9]{11}$' then
    raise exception using errcode = '22023', message = 'El RUC debe contener 11 dígitos';
  end if;
  if not coalesce(p_accepts_pickup, false) and not coalesce(p_accepts_delivery, false) then
    raise exception using errcode = '22023', message = 'Habilita recojo o delivery';
  end if;
  if coalesce(p_accepts_cash_pickup, false) and not coalesce(p_accepts_pickup, false) then
    raise exception using errcode = '22023', message = 'El efectivo al recoger requiere habilitar recojo';
  end if;
  if coalesce(p_default_preparation_minutes, 0) not between 1 and 1440 then
    raise exception using errcode = '22023', message = 'El tiempo de preparación debe estar entre 1 y 1440 minutos';
  end if;
  if pg_catalog.jsonb_typeof(coalesce(p_business_hours, 'null'::jsonb)) <> 'array' or
     pg_catalog.jsonb_array_length(p_business_hours) <> 7 then
    raise exception using errcode = '22023', message = 'Debes enviar los siete días del horario semanal';
  end if;
  if pg_catalog.jsonb_typeof(coalesce(p_schedule_exceptions, 'null'::jsonb)) <> 'array' then
    raise exception using errcode = '22023', message = 'Las fechas especiales no son válidas';
  end if;

  if exists (
    select 1
    from pg_catalog.jsonb_to_recordset(p_business_hours)
      as h(weekday integer, opens_at text, closes_at text, is_closed boolean)
    group by h.weekday
    having h.weekday not between 1 and 7 or pg_catalog.count(*) <> 1
  ) then
    raise exception using errcode = '22023', message = 'Cada día de la semana debe aparecer una sola vez';
  end if;

  if exists (
    select 1
    from pg_catalog.jsonb_to_recordset(p_business_hours)
      as h(weekday integer, opens_at text, closes_at text, is_closed boolean)
    where (coalesce(h.is_closed, false) and
           (nullif(pg_catalog.btrim(coalesce(h.opens_at, '')), '') is not null or
            nullif(pg_catalog.btrim(coalesce(h.closes_at, '')), '') is not null))
       or (not coalesce(h.is_closed, false) and
           (nullif(pg_catalog.btrim(coalesce(h.opens_at, '')), '') is null or
            nullif(pg_catalog.btrim(coalesce(h.closes_at, '')), '') is null or
            h.closes_at::time <= h.opens_at::time))
  ) then
    raise exception using errcode = '22023', message = 'Revisa las horas de apertura y cierre';
  end if;

  if exists (
    select 1
    from pg_catalog.jsonb_to_recordset(p_schedule_exceptions)
      as e(local_date text, opens_at text, closes_at text, is_closed boolean, public_message text)
    group by e.local_date
    having nullif(pg_catalog.btrim(coalesce(e.local_date, '')), '') is null or pg_catalog.count(*) <> 1
  ) then
    raise exception using errcode = '22023', message = 'Cada fecha especial debe aparecer una sola vez';
  end if;

  if exists (
    select 1
    from pg_catalog.jsonb_to_recordset(p_schedule_exceptions)
      as e(local_date text, opens_at text, closes_at text, is_closed boolean, public_message text)
    where e.local_date::date is null
       or (coalesce(e.is_closed, false) and
           (nullif(pg_catalog.btrim(coalesce(e.opens_at, '')), '') is not null or
            nullif(pg_catalog.btrim(coalesce(e.closes_at, '')), '') is not null))
       or (not coalesce(e.is_closed, false) and
           (nullif(pg_catalog.btrim(coalesce(e.opens_at, '')), '') is null or
            nullif(pg_catalog.btrim(coalesce(e.closes_at, '')), '') is null or
            e.closes_at::time <= e.opens_at::time))
  ) then
    raise exception using errcode = '22023', message = 'Revisa las fechas y horas especiales';
  end if;

  update public.stores
  set name = pg_catalog.btrim(p_store_name),
      address = nullif(pg_catalog.btrim(coalesce(p_address, '')), ''),
      district = nullif(pg_catalog.btrim(coalesce(p_district, '')), ''),
      updated_at = pg_catalog.statement_timestamp()
  where id = p_store_id;

  insert into public.store_settings (
    store_id, display_name, legal_name, tax_id, phone, whatsapp_phone,
    currency, locale, accepts_pickup, accepts_delivery, accepts_cash_pickup,
    accepts_yape, accepts_plin, accepts_card, default_preparation_minutes,
    updated_by, updated_at
  ) values (
    p_store_id,
    pg_catalog.btrim(p_display_name),
    nullif(pg_catalog.btrim(coalesce(p_legal_name, '')), ''),
    nullif(pg_catalog.btrim(coalesce(p_tax_id, '')), ''),
    nullif(pg_catalog.btrim(coalesce(p_phone, '')), ''),
    nullif(pg_catalog.btrim(coalesce(p_whatsapp_phone, '')), ''),
    'PEN', 'es-PE',
    coalesce(p_accepts_pickup, false),
    coalesce(p_accepts_delivery, false),
    coalesce(p_accepts_cash_pickup, false),
    coalesce(p_accepts_yape, false),
    coalesce(p_accepts_plin, false),
    coalesce(p_accepts_card, false),
    p_default_preparation_minutes,
    v_actor,
    pg_catalog.statement_timestamp()
  )
  on conflict (store_id) do update set
    display_name = excluded.display_name,
    legal_name = excluded.legal_name,
    tax_id = excluded.tax_id,
    phone = excluded.phone,
    whatsapp_phone = excluded.whatsapp_phone,
    accepts_pickup = excluded.accepts_pickup,
    accepts_delivery = excluded.accepts_delivery,
    accepts_cash_pickup = excluded.accepts_cash_pickup,
    accepts_yape = excluded.accepts_yape,
    accepts_plin = excluded.accepts_plin,
    accepts_card = excluded.accepts_card,
    default_preparation_minutes = excluded.default_preparation_minutes,
    updated_by = excluded.updated_by,
    updated_at = excluded.updated_at;

  delete from public.store_business_hours where store_id = p_store_id;
  insert into public.store_business_hours (
    store_id, weekday, opens_at, closes_at, is_closed, updated_at
  )
  select
    p_store_id,
    h.weekday::smallint,
    case when coalesce(h.is_closed, false) then null else h.opens_at::time end,
    case when coalesce(h.is_closed, false) then null else h.closes_at::time end,
    coalesce(h.is_closed, false),
    pg_catalog.statement_timestamp()
  from pg_catalog.jsonb_to_recordset(p_business_hours)
    as h(weekday integer, opens_at text, closes_at text, is_closed boolean);

  delete from public.store_schedule_exceptions where store_id = p_store_id;
  insert into public.store_schedule_exceptions (
    store_id, local_date, opens_at, closes_at, is_closed, public_message,
    created_by, updated_at
  )
  select
    p_store_id,
    e.local_date::date,
    case when coalesce(e.is_closed, false) then null else e.opens_at::time end,
    case when coalesce(e.is_closed, false) then null else e.closes_at::time end,
    coalesce(e.is_closed, false),
    nullif(pg_catalog.btrim(coalesce(e.public_message, '')), ''),
    v_actor,
    pg_catalog.statement_timestamp()
  from pg_catalog.jsonb_to_recordset(p_schedule_exceptions)
    as e(local_date text, opens_at text, closes_at text, is_closed boolean, public_message text);

  v_result := pg_catalog.jsonb_build_object(
    'store_id', p_store_id,
    'business_hours_count', pg_catalog.jsonb_array_length(p_business_hours),
    'schedule_exceptions_count', pg_catalog.jsonb_array_length(p_schedule_exceptions),
    'idempotent_replay', false
  );

  insert into private.admin_operation_results (
    store_id, operation_id, operation_type, actor_user_id, result
  ) values (
    p_store_id, p_operation_id, 'save_store_configuration', v_actor, v_result
  );

  return v_result;
end;
$function$;

create or replace function public.save_store_configuration(
  p_store_id uuid,
  p_store_name text,
  p_address text,
  p_district text,
  p_display_name text,
  p_legal_name text,
  p_tax_id text,
  p_phone text,
  p_whatsapp_phone text,
  p_accepts_pickup boolean,
  p_accepts_delivery boolean,
  p_accepts_cash_pickup boolean,
  p_accepts_yape boolean,
  p_accepts_plin boolean,
  p_accepts_card boolean,
  p_default_preparation_minutes integer,
  p_business_hours jsonb,
  p_schedule_exceptions jsonb,
  p_operation_id uuid
)
returns jsonb
language sql
security invoker
set search_path = ''
as $function$
  select private.save_store_configuration(
    p_store_id, p_store_name, p_address, p_district, p_display_name,
    p_legal_name, p_tax_id, p_phone, p_whatsapp_phone,
    p_accepts_pickup, p_accepts_delivery, p_accepts_cash_pickup,
    p_accepts_yape, p_accepts_plin, p_accepts_card,
    p_default_preparation_minutes, p_business_hours,
    p_schedule_exceptions, p_operation_id
  );
$function$;

revoke all on function public.save_store_configuration(uuid,text,text,text,text,text,text,text,text,boolean,boolean,boolean,boolean,boolean,boolean,integer,jsonb,jsonb,uuid) from public;
revoke all on function public.save_store_configuration(uuid,text,text,text,text,text,text,text,text,boolean,boolean,boolean,boolean,boolean,boolean,integer,jsonb,jsonb,uuid) from anon;
grant execute on function public.save_store_configuration(uuid,text,text,text,text,text,text,text,text,boolean,boolean,boolean,boolean,boolean,boolean,integer,jsonb,jsonb,uuid) to authenticated;
