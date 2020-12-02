using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/**
 * Esta clase va a hacer a modo de "gameManager", va a ser la que se encarga de coordinar todo dentro de nuestra escena,
 * desde la generacion de partículas como la gestion del pool y el algoritmo de hash spacial, tendremos dos Diccionarios,
 * uno para el pool donde tendremos guardado en el Start el doble del valor definido, en numberParticles, para así tener disponibilidad
 * para un posible incremento o un chorro. Pondremos un chorro que lance las partículas en sucesión.
 */
public class ParticleEmitter : Singleton<ParticleEmitter>
{
    Dictionary<string, List<GameObject>> pool;
    Transform poolParent;
    public GameObject particlePrefab;
    public static Dictionary<float, List<Particle>> spatialHash
    {
        get; set;
    }
    public static Vector3 gravity
    {
        get; set;
    }
    public Vector3 gravedad;

    public Vector2 limites_x;
    public Vector2 limites_y;
    public Vector2 limites_z;
    public int numeroParticulas;
    //Este valor nos indica lo mucho que queremos controlar el espacio
    public static int numeroCeldasHash = 1000;
    //Aqui damos una franja para montar el cubo
    public static Vector2 limit_x { get; set; }
    public static Vector2 limit_y { get; set; }
    public static Vector2 limit_z { get; set; }

    public static int spawnNumber { get; set; }

    public List<Particle> allParticles = new List<Particle>();

    public float distanciaEntreParticulas = 0.1f;
    public static float distanceBetweenParticles;
    void Start()
    {
        allParticles = new List<Particle>();
        gravity = gravedad;
        limit_x = limites_x;
        limit_y = limites_y;
        limit_z = limites_z;
        distanceBetweenParticles = distanciaEntreParticulas;
        spawnNumber = numeroParticulas;

        Load(particlePrefab, spawnNumber * 2);

    }

    // Update is called once per frame
    void Update()
    {
        //seteamos las variables staticas con los valores que hemos hecho en el input public, asi podemos editar en una sola
        //clase y a su vez tenemos disponibles todas las variables en el código. Esto se ha hecho por comodidad, así solo
        //tenemos que editar desde el editor de unity en el emitter
        gravity = gravedad;
        limit_x = limites_x;
        limit_y = limites_y;
        limit_z = limites_z;
        spawnNumber = numeroParticulas;

        if (Input.GetKeyDown(KeyCode.Space))
        {
           
               StartCoroutine("createBunchOfParticles");

            
        }
    }
    IEnumerator createBunchOfParticles()
    {
        for (int i = 0; i < numeroParticulas; i++)
        {
            yield return new WaitForSeconds(0.7f);
            Spawn(particlePrefab, new Vector3(3, 3, 3), Quaternion.identity, 1);
        }
    }

    private void FixedUpdate()
    {
        
        //primero vamos a tomar los valores que tenemos ya generados del hash
        foreach (Particle p in allParticles)
        {
            p.moveParticle();
        }
        //limpiamos hash y repopulamos con nuevas posiciones para el siguiente cálculo
        spatialHash = new Dictionary<float, List<Particle>>();
        foreach (Particle p in allParticles)
        {
            InstantiateInSpatialHash(p);
        }




    }

    private void Awake()
    {
        poolParent = new GameObject("Pool").transform;
        pool = new Dictionary<string, List<GameObject>>();
    }

    // Precarga del objeto
    public void Load(GameObject prefab, int amount = 1)
    {

        if (!pool.ContainsKey(prefab.name))
        {
            pool[prefab.name] = new List<GameObject>();
        }

        for (int i = 0; i < amount; i++)
        {
            var go = Instantiate(prefab);
            go.name = prefab.name;
            go.SetActive(false);
            go.transform.parent = poolParent;
            pool[prefab.name].Add(go);
        }
    }

    public GameObject Spawn(GameObject prefab)
    {
        //Para diferenciar a nuestras partículas tendremos el getInstanceID que es unique garantizado
        return Spawn(prefab, prefab.transform.position, prefab.transform.rotation);
    }

    public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, int velocity=0)
    {
        if (!pool.ContainsKey(prefab.name) || pool[prefab.name].Count == 0)
        {
            Load(prefab);
        }
        var go = pool[prefab.name][0];
        pool[prefab.name].RemoveAt(0);
        go.transform.parent = null;
        var t = go.GetComponent<Transform>();
        t.position = pos;
        t.rotation = rot;
        go.SetActive(true);
        InstantiateInSpatialHash(go);
        //Añadimos a un listado general que nos va a servir para ir regenerando el spatialHashMap con cada iteracion en el FixedUpdate
        Particle p = (Particle)go.GetComponent(typeof(Particle));
        p.velocity = new Vector3(Random.Range(-1,1), Random.Range(-1, 1), Random.Range(-1, 1)).normalized*velocity;
        allParticles.Add(p);
        return go;
    }

    public void Despawn(GameObject go)
    {
        go.SetActive(false);
        go.transform.parent = poolParent;
        pool[go.name].Add(go);
    }

    public static int calculateHashPosition(Vector3 pos)
    {
        //Estos 3 primos son lo más diferentes posibles para que haya dispersión total y no haya colisión de claves
        int prime1 = 73856093;
        int prime2 = 19349663;
        int prime3 = 83492791;

        //Multiplicamos por nuestras coordenadas
        int aux1 = prime1 * (int)pos.x;
        int aux2 = prime2 * (int)pos.y;
        int aux3 = prime3 * (int)pos.z;

        int result = aux1 ^ aux2 ^ aux3;
        int r = result % numeroCeldasHash;

        //Si nos da negativo, tenemos que dar la vuelta
        return r < 0 ? r + numeroCeldasHash : r;

    }

    public void InstantiateInSpatialHash(GameObject p)
    {
        Particle particle = (Particle)p.GetComponent(typeof(Particle));
        InstantiateInSpatialHash(particle);
    }

    public void InstantiateInSpatialHash(Particle particle)
    {

        int hashID = calculateHashPosition(particle.transform.position);

        particle.hashId = hashID;
        if (!spatialHash.ContainsKey(hashID))
        {
            List<Particle> l = new List<Particle>();
            l.Add(particle);
            spatialHash.Add(hashID, l);
        }
        else
        {
            spatialHash[hashID].Add(particle);
        }
    }

    //Calculamos los posibles vecinos, para ello usaremos consultas LINQ
    public static List<Particle> findMyNeighbourInHash(Particle p)
    {
        float h = distanceBetweenParticles;
        Vector3 position = p.transform.position; 
        float hashID = p.hashId;
        List<Particle> neighbours = new List<Particle>();

        //Hemos añadido los posibles vecinos de nuestra casilla, ahora vamos a por los de las casillas consecutivas
        neighbours.AddRange(spatialHash[hashID]);
        
        Vector3 neighbour2 = new Vector3(position.x, position.y, position.z + h);
        Vector3 neighbour3 = new Vector3(position.x, position.y, position.z - h);
        Vector3 neighbour4 = new Vector3(position.x, position.y + h, position.z);
        Vector3 neighbour5 = new Vector3(position.x, position.y + h, position.z + h);
        Vector3 neighbour6 = new Vector3(position.x, position.y + h, position.z - h);
        Vector3 neighbour7 = new Vector3(position.x, position.y - h, position.z);
        Vector3 neighbour8 = new Vector3(position.x, position.y - h, position.z + h);
        Vector3 neighbour9 = new Vector3(position.x, position.y - h, position.z - h);
        Vector3 neighbour10 = new Vector3(position.x + h, position.y, position.z);
        Vector3 neighbour11 = new Vector3(position.x + h, position.y, position.z + h);
        Vector3 neighbour12 = new Vector3(position.x + h, position.y, position.z - h);
        Vector3 neighbour13 = new Vector3(position.x + h, position.y + h, position.z);
        Vector3 neighbour14 = new Vector3(position.x + h, position.y + h, position.z + h);
        Vector3 neighbour15 = new Vector3(position.x + h, position.y + h, position.z - h);
        Vector3 neighbour16 = new Vector3(position.x + h, position.y - h, position.z);
        Vector3 neighbour17 = new Vector3(position.x + h, position.y - h, position.z + h);
        Vector3 neighbour18 = new Vector3(position.x + h, position.y - h, position.z - h);
        Vector3 neighbour19 = new Vector3(position.x - h, position.y, position.z);
        Vector3 neighbour20 = new Vector3(position.x - h, position.y, position.z + h);
        Vector3 neighbour21 = new Vector3(position.x - h, position.y, position.z - h);
        Vector3 neighbour22 = new Vector3(position.x - h, position.y + h, position.z);
        Vector3 neighbour23 = new Vector3(position.x - h, position.y + h, position.z + h);
        Vector3 neighbour24 = new Vector3(position.x - h, position.y + h, position.z - h);
        Vector3 neighbour25 = new Vector3(position.x - h, position.y - h, position.z);
        Vector3 neighbour26 = new Vector3(position.x - h, position.y - h, position.z + h);
        Vector3 neighbour27 = new Vector3(position.x - h, position.y - h, position.z - h);

        float pos2 = calculateHashPosition(neighbour2);
        float pos3 = calculateHashPosition(neighbour3);
        float pos4 = calculateHashPosition(neighbour4);
        float pos5 = calculateHashPosition(neighbour5);
        float pos6 = calculateHashPosition(neighbour6);
        float pos7 = calculateHashPosition(neighbour7);
        float pos8 = calculateHashPosition(neighbour8);
        float pos9 = calculateHashPosition(neighbour9);
        float pos10 = calculateHashPosition(neighbour10);
        float pos11 = calculateHashPosition(neighbour11);
        float pos12 = calculateHashPosition(neighbour12);
        float pos13 = calculateHashPosition(neighbour13);
        float pos14 = calculateHashPosition(neighbour14);
        float pos15 = calculateHashPosition(neighbour15);
        float pos16 = calculateHashPosition(neighbour16);
        float pos17 = calculateHashPosition(neighbour17);
        float pos18 = calculateHashPosition(neighbour18);
        float pos19 = calculateHashPosition(neighbour19);
        float pos20 = calculateHashPosition(neighbour20);
        float pos21 = calculateHashPosition(neighbour21);
        float pos22 = calculateHashPosition(neighbour22);
        float pos23 = calculateHashPosition(neighbour23);
        float pos24 = calculateHashPosition(neighbour24);
        float pos25 = calculateHashPosition(neighbour25);
        float pos26 = calculateHashPosition(neighbour26);
        float pos27 = calculateHashPosition(neighbour27);



        neighbours.AddRange(spatialHash.ContainsKey(pos2)?spatialHash[pos2]:new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos3) ? spatialHash[pos3] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos4) ? spatialHash[pos4] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos5) ? spatialHash[pos5] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos6) ? spatialHash[pos6] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos7) ? spatialHash[pos7] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos8) ? spatialHash[pos8] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos9) ? spatialHash[pos9] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos10) ? spatialHash[pos10] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos11) ? spatialHash[pos11] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos12) ? spatialHash[pos12] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos13) ? spatialHash[pos13] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos14) ? spatialHash[pos14] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos15) ? spatialHash[pos15] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos16) ? spatialHash[pos16] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos17) ? spatialHash[pos17] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos18) ? spatialHash[pos18] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos19) ? spatialHash[pos19] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos20) ? spatialHash[pos20] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos21) ? spatialHash[pos21] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos22) ? spatialHash[pos22] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos23) ? spatialHash[pos23] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos24) ? spatialHash[pos24] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos25) ? spatialHash[pos25] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos26) ? spatialHash[pos26] : new List<Particle>());
        neighbours.AddRange(spatialHash.ContainsKey(pos27) ? spatialHash[pos27] : new List<Particle>());



        //AQUI AÑADIMOS TODAS LAS CASILAS DEL ESPACIO DIVIDIDO EN CUADROS, SIENDO EL 0,0,0 EL NUESTRO
        //EL RESTO SON LAS CUADRICULAS
        //[ ][ ][ ]
        //[ ][p][ ]
        //[ ][ ][ ]
        //en 3 dimensiones da 27 cuadriculas y por tanto da 26 posibles casillas sumando la nuestra
        //Ahora vamos a hacer una consulta en LINQ que nos los filtre por cercanía
        neighbours=neighbours.Where(x => (x.transform.position - position ).magnitude<=h).ToList<Particle>();
        /*foreach(Particle x in neighbours)
        {

            Debug.Log(x.GetInstanceID());
        }*/
        neighbours= neighbours.Where(x => x.GetInstanceID() != p.GetInstanceID()).GroupBy(x => x.GetInstanceID()).Select(x=> x.First()).ToList();
        return neighbours;
    }
}


