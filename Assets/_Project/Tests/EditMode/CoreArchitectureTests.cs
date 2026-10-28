// -----------------------------------------------------------------------------
// NostalgIA — Pruebas de modo de edición (NostalgIA.Tests.EditMode)
// Archivo: CoreArchitectureTests.cs
// Propósito: verificar el bus de eventos, el localizador de servicios, el
// registro de reinicio y la descripción de daño (tareas F1-A01, A11 y A12).
// -----------------------------------------------------------------------------

using NostalgIA.Core.Damage;
using NostalgIA.Core.Events;
using NostalgIA.Core.Lifecycle;
using NostalgIA.Core.Services;
using NUnit.Framework;
using UnityEngine;

namespace NostalgIA.Tests.EditMode
{
    /// <summary>Pruebas unitarias de la infraestructura de <c>NostalgIA.Core</c>.</summary>
    public sealed class CoreArchitectureTests
    {
        /// <summary>Evento exclusivo de las pruebas, para no interferir con los del juego.</summary>
        private readonly struct TestEvent : IGameEvent
        {
            public readonly int Value;
            public TestEvent(int value) => Value = value;
        }

        /// <summary>Contrato exclusivo de las pruebas del localizador.</summary>
        private interface ITestService { }

        private sealed class TestService : ITestService { }

        private sealed class TestResettable : IResettable
        {
            public int Resets;
            public void ResetState() => Resets++;
        }

        private int received;
        private int calls;

        private void Handler(in TestEvent evt)
        {
            received = evt.Value;
            calls++;
        }

        /// <summary>Deja los buses y el registro limpios entre pruebas.</summary>
        [SetUp]
        public void Limpiar()
        {
            EventBus<TestEvent>.Clear();
            received = 0;
            calls = 0;
        }

        /// <summary>Restaura el estado global.</summary>
        [TearDown]
        public void Restaurar()
        {
            EventBus<TestEvent>.Clear();
        }

        /// <summary>Un oyente suscrito recibe la carga útil exacta una sola vez.</summary>
        [Test]
        public void EventBus_EntregaElEventoAlSuscriptor()
        {
            EventBus<TestEvent>.Subscribe(Handler);
            EventBus<TestEvent>.Raise(new TestEvent(42));
            Assert.AreEqual(42, received);
            Assert.AreEqual(1, calls);
        }

        /// <summary>Tras cancelar la suscripción el oyente deja de recibir eventos.</summary>
        [Test]
        public void EventBus_UnsubscribeDetieneLaEntrega()
        {
            EventBus<TestEvent>.Subscribe(Handler);
            EventBus<TestEvent>.Unsubscribe(Handler);
            EventBus<TestEvent>.Raise(new TestEvent(7));
            Assert.AreEqual(0, calls);
            Assert.IsFalse(EventBus<TestEvent>.HasSubscribers);
        }

        /// <summary>Publicar sin oyentes no lanza excepciones.</summary>
        [Test]
        public void EventBus_PublicarSinOyentesEsSeguro()
        {
            Assert.DoesNotThrow(() => EventBus<TestEvent>.Raise(new TestEvent(1)));
        }

        /// <summary>
        /// Publicar no asigna memoria en el montón (requisito de 0 B de GC por frame).
        /// </summary>
        [Test]
        public void EventBus_PublicarNoAsignaMemoria()
        {
            EventBus<TestEvent>.Subscribe(Handler);
            EventBus<TestEvent>.Raise(new TestEvent(1));   // calentamiento (compilación JIT)

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) EventBus<TestEvent>.Raise(new TestEvent(i));
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, "EventBus.Raise no debe asignar memoria.");
        }

        /// <summary>El localizador devuelve la instancia registrada y solo la retira su dueño.</summary>
        [Test]
        public void ServiceLocator_RegistraYRetiraSoloLaInstanciaPropia()
        {
            var first = new TestService();
            var second = new TestService();
            ServiceLocator.Register<ITestService>(first);
            Assert.IsTrue(ServiceLocator.TryGet(out ITestService found));
            Assert.AreSame(first, found);

            ServiceLocator.Register<ITestService>(second);
            ServiceLocator.Unregister<ITestService>(first);   // no es la registrada: no debe borrar
            Assert.AreSame(second, ServiceLocator.Get<ITestService>());

            ServiceLocator.Unregister<ITestService>(second);
            Assert.IsFalse(ServiceLocator.TryGet(out ITestService _));
        }

        /// <summary>Un objeto de Unity destruido deja de considerarse registrado.</summary>
        [Test]
        public void ServiceLocator_IgnoraObjetosDestruidos()
        {
            var go = new GameObject("ServicioTemporal");
            ServiceLocator.Register(go.transform);
            Object.DestroyImmediate(go);
            Assert.IsFalse(ServiceLocator.TryGet(out Transform _));
        }

        /// <summary>El registro de reinicio llama a cada objeto una vez y acepta retiros.</summary>
        [Test]
        public void ResettableRegistry_ReiniciaLosRegistrados()
        {
            var a = new TestResettable();
            var b = new TestResettable();
            ResettableRegistry.Register(a);
            ResettableRegistry.Register(a);   // duplicado ignorado
            ResettableRegistry.Register(b);
            ResettableRegistry.Unregister(b);

            ResettableRegistry.ResetAll();
            Assert.AreEqual(1, a.Resets);
            Assert.AreEqual(0, b.Resets);
            ResettableRegistry.Unregister(a);
        }

        /// <summary>Los constructores de daño fijan tipo, cantidad y causa.</summary>
        [Test]
        public void DamageInfo_ConstructoresDescribenElGolpe()
        {
            DamageInfo normal = DamageInfo.Normal(2, Vector3.one);
            Assert.AreEqual(DamageType.Normal, normal.Type);
            Assert.AreEqual(2, normal.Amount);
            Assert.AreEqual(DeathCause.Hearts, normal.Cause);

            DamageInfo kill = DamageInfo.Instakill(Vector3.zero, DeathCause.Void);
            Assert.AreEqual(DamageType.Instakill, kill.Type);
            Assert.AreEqual(DeathCause.Void, kill.Cause);

            Assert.AreEqual(0, DamageInfo.Normal(-5, Vector3.zero).Amount, "La cantidad negativa se fuerza a cero.");
        }
    }
}
