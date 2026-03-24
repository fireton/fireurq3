import { coreMigrationStatus } from "@fireurq/core";
import { playerMigrationStatus } from "@fireurq/player";

export function App() {
  return (
    <main className="shell">
      <section className="hero">
        <p className="eyebrow">FireURQ Web</p>
        <h1>TypeScript migration workspace</h1>
        <p className="lede">
          The web player shell is in place. The next step is porting the URQL
          parser, compiler, and VM into the local TypeScript packages.
        </p>
      </section>

      <section className="grid">
        <article className="card">
          <h2>Core</h2>
          <p>{coreMigrationStatus.summary}</p>
          <ul>
            {coreMigrationStatus.nextSteps.map((step) => (
              <li key={step}>{step}</li>
            ))}
          </ul>
        </article>

        <article className="card">
          <h2>Player</h2>
          <p>{playerMigrationStatus.summary}</p>
          <ul>
            {playerMigrationStatus.nextSteps.map((step) => (
              <li key={step}>{step}</li>
            ))}
          </ul>
        </article>
      </section>
    </main>
  );
}
