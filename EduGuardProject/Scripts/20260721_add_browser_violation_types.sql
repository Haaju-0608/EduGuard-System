-- Run on Supabase/Postgres before deploying the Browser Violation backend.
-- Adds the new browser-violation labels to the existing violation_type enum.
-- Postgres requires each ALTER TYPE ... ADD VALUE to run outside a surrounding
-- transaction block, so run these as separate statements (not wrapped in BEGIN/COMMIT).
--
-- NOTE: labels are PascalCase ("TabSwitch") to match the C# enum member names via
-- [PgName], unlike the older AI violation labels which use SCREAMING_SNAKE_CASE.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_enum WHERE enumlabel = 'TabSwitch' AND enumtypid = 'public.violation_type'::regtype
    ) THEN
        ALTER TYPE public.violation_type ADD VALUE 'TabSwitch';
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_enum WHERE enumlabel = 'WindowBlur' AND enumtypid = 'public.violation_type'::regtype
    ) THEN
        ALTER TYPE public.violation_type ADD VALUE 'WindowBlur';
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_enum WHERE enumlabel = 'ExitFullscreen' AND enumtypid = 'public.violation_type'::regtype
    ) THEN
        ALTER TYPE public.violation_type ADD VALUE 'ExitFullscreen';
    END IF;
END $$;
