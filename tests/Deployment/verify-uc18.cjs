const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');

const repo = path.resolve(__dirname, '../..');
const artifacts = path.join(repo, 'artifacts', 'uc18');
fs.mkdirSync(artifacts, { recursive: true });
const root = fs.mkdtempSync(path.join(artifacts, 'deploy-check-'));
const secret = "p'a$ss\\tail";
const variables = {
    DEPLOY_SHA: 'a'.repeat(40), IMAGE_REPOSITORY: 'ghcr.io/example/uc18', REGISTRY_USER: 'sandbox', REGISTRY_TOKEN: 'sandbox-only',
    DB_CONNECTION_STRING: 'Host=postgres;Database=uc18_test;Username=uc18_test;Password=' + secret,
    REDIS_CONNECTION_STRING: 'redis:6379', JWT_ISSUER: 'uc18-test', JWT_AUDIENCE: 'uc18-test', JWT_SECRET: secret,
    JWT_EXPIRE_MIN: '60', RABBITMQ_HOST: 'rabbitmq', RABBITMQ_VHOST: '/', RABBITMQ_PORT: '5672', RABBITMQ_USER: 'test', RABBITMQ_PASS: secret,
    SETTLEMENT_WRITES_ENABLED: 'true', SETTLEMENT_DISPATCH_ENABLED: 'false', SETTLEMENT_PREVIEW_SIGNING_KEY: secret,
    RECONCILIATION_ENABLED: 'false', RECONCILIATION_WRITES_ENABLED: 'false', RECONCILIATION_DISPATCH_ENABLED: 'false'
};
const workflow = fs.readFileSync(path.join(repo, '.github', 'workflows', 'deploy.yml'), 'utf8').replaceAll('\r\n', '\n');
const marker = '          script: |\n';
const start = workflow.lastIndexOf(marker);
assert.ok(start >= 0, 'Không tìm thấy SSH deploy script trong workflow.');
const deployScript = workflow.slice(start + marker.length).split('\n').map(line => line.replace(/^ {12}/, '')).join('\n');

function run(command, args, options = {}) {
    const result = spawnSync(command, args, { cwd: repo, encoding: 'utf8', windowsHide: true, ...options });
    if (result.error) throw result.error;
    if (result.status !== 0 && !options.allowFailure) throw new Error(command + ' failed: ' + result.stderr);
    return result;
}

try {
    fs.writeFileSync(path.join(root, 'remote-deploy.sh'), deployScript);
    run('docker', ['run', '--rm', '--network', 'none', '--mount', 'type=bind,source=' + root + ',target=/check,readonly', '--entrypoint', 'sh', 'postgres:16-alpine', '-n', '/check/remote-deploy.sh']);
    for (const scenario of [
        { name: 'migration-failed', migration: false, verification: true },
        { name: 'verification-failed', migration: true, verification: false },
        { name: 'ready', migration: true, verification: true }
    ]) {
        const success = scenario.migration && scenario.verification;
        const fixture = path.join(root, scenario.name);
        fs.mkdirSync(fixture);
        fs.mkdirSync(path.join(fixture, 'bin'));
        fs.writeFileSync(path.join(fixture, 'remote-deploy.sh'), deployScript);
        fs.copyFileSync(path.join(repo, 'docker-compose.yml'), path.join(fixture, 'docker-compose.yml'));
        fs.writeFileSync(path.join(fixture, 'sports-venue-cb204-firebase-adminsdk-fbsvc-7f5ec12fb1.json'), '{}');
        const previousEnvironment = 'PREVIOUS_DEPLOY=keep\n';
        fs.writeFileSync(path.join(fixture, '.env'), previousEnvironment);
        fs.writeFileSync(path.join(fixture, 'test.env'), Object.entries({ ...variables,
            DEPLOY_TEST_MIGRATION_RESULT: scenario.migration ? 'pass' : 'fail',
            DEPLOY_TEST_VERIFY_RESULT: scenario.verification ? 'pass' : 'fail'
        }).map(([key, value]) => key + '=' + value).join('\n') + '\n');
        fs.writeFileSync(path.join(fixture, 'bin', 'docker'), `#!/bin/sh
printf '%s\\n' "$*" >> /fixture/commands.log
case "$1" in
  login) cat >/dev/null ;;
  compose) case " $* " in
    *'Migrations__VerifyOnly=false'*) [ "$DEPLOY_TEST_MIGRATION_RESULT" = pass ] || exit 8 ;;
    *'Migrations__VerifyOnly=true'*) [ "$DEPLOY_TEST_VERIFY_RESULT" = pass ] || exit 9 ;;
  esac ;;
esac
`);
        fs.writeFileSync(path.join(fixture, 'bin', 'curl'), `#!/bin/sh
while [ "$#" -gt 0 ]; do
  if [ "$1" = -o ]; then shift; printf '%s' '{"paths":{"/api/v1/webhooks/sepay/test":{},"/api/v1/clubs/test/reconciliations":{}}}' > "$1"; exit 0; fi
  shift
done
exit 1
`);
        const result = run('docker', ['run', '--rm', '--network', 'none', '--mount', 'type=bind,source=' + fixture + ',target=/fixture', '--mount', 'type=bind,source=' + fixture + ',target=/root/badmintonclub', '--env-file', path.join(fixture, 'test.env'), '--entrypoint', 'sh', 'postgres:16-alpine', '-c', 'chmod +x /fixture/bin/*; export PATH="/fixture/bin:$PATH"; sh /fixture/remote-deploy.sh'], { allowFailure: true });
        assert.equal(result.status === 0, success);
        const commands = fs.readFileSync(path.join(fixture, 'commands.log'), 'utf8');
        const migrationCommand = 'run --rm --no-deps -e Seeding__Enabled=false -e Migrations__VerifyOnly=false migrator';
        const verificationCommand = 'run --rm --no-deps -e Migrations__VerifyOnly=true migrator';
        assert.ok(commands.includes(migrationCommand));
        assert.equal(commands.includes(verificationCommand), scenario.migration);
        if (success) assert.ok(commands.indexOf(migrationCommand) < commands.indexOf(verificationCommand));
        assert.equal(commands.includes('up -d --no-deps --force-recreate api'), success);
        assert.equal(fs.readFileSync(path.join(fixture, success ? '.env.before-' + variables.DEPLOY_SHA : '.env'), 'utf8'), previousEnvironment);
        assert.equal(commands.includes('prune'), false);
        const cleanEnvironment = { ...process.env };
        for (const key of [...Object.keys(variables), 'API_IMAGE', 'MIGRATOR_IMAGE', 'RECONCILIATION_EVIDENCE_DIRECTORY', 'SEPAY_KEY_DIRECTORY']) delete cleanEnvironment[key];
        const config = JSON.parse(run('docker', ['compose', '--profile', 'migration', '--project-directory', fixture, '--env-file', path.join(fixture, success ? '.env' : '.env.next'), '-f', path.join(fixture, 'docker-compose.yml'), 'config', '--format', 'json'], { env: cleanEnvironment }).stdout);
        assert.equal(config.services.api.environment.ConnectionStrings__Database.replaceAll('$$', '$'), variables.DB_CONNECTION_STRING);
        assert.equal(config.services.api.environment.Jwt__SecretKey.replaceAll('$$', '$'), secret);
        assert.equal(config.services.api.environment.MasstransitConfiguration__Password.replaceAll('$$', '$'), secret);
        assert.equal(config.services.api.environment.Settlement__WritesEnabled, 'true');
        assert.equal(config.services.api.environment.Settlement__DispatchEnabled, 'false');
        assert.equal(config.services.api.environment.Settlement__PreviewSigningKey.replaceAll('$$', '$'), secret);
        assert.equal(config.services.api.environment.Seeding__Enabled, 'false');
        assert.equal(config.services.migrator.environment.Seeding__Enabled, 'false');
        assert.equal(config.services.api.environment.Reconciliation__Enabled, 'false');
        assert.equal(config.services.api.environment.Reconciliation__WritesEnabled, 'false');
        assert.equal(config.services.api.environment.Reconciliation__DispatchEnabled, 'false');
        assert.equal(config.services.api.image, 'ghcr.io/example/uc18:sha-' + variables.DEPLOY_SHA);
        assert.equal(config.services.migrator.image, 'ghcr.io/example/uc18-migrator:sha-' + variables.DEPLOY_SHA);
        fs.writeFileSync(path.join(fixture, 'literal.yml'), `services:
  literal:
    image: postgres:16-alpine
    network_mode: none
    environment:
      TEST_VALUE: \${DB_CONNECTION_STRING}
    entrypoint: ${JSON.stringify(['sh', '-c', 'printf "%s" "$$TEST_VALUE"'])}
`);
        const literal = run('docker', ['compose', '--project-directory', fixture, '--env-file', path.join(fixture, success ? '.env' : '.env.next'), '-f', path.join(fixture, 'literal.yml'), 'run', '--rm', '--no-deps', 'literal'], { env: cleanEnvironment });
        assert.equal(literal.stdout, variables.DB_CONNECTION_STRING);
    }
    console.log('UC18 deploy: tự migration trước API, không seeder; migration/schema lỗi giữ API và .env cũ; thành công dùng đúng SHA/cờ tắt và giữ literal secret.');
} finally {
    const resolved = path.resolve(root);
    if (!resolved.startsWith(path.resolve(artifacts) + path.sep)) throw new Error('Fixture vượt thư mục artifacts.');
    fs.rmSync(resolved, { recursive: true, force: true });
}
