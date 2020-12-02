using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Particle : MonoBehaviour
{
    private Vector3 position;
    public Vector3 velocity
    {
        get;set;
    }
    private Vector3 acceleration;
    public float gas_constant = 1;
    public float viscosity_constant = 1;
    private float h;
    private float mass = 0.01f;
    private float density=0f;
    public List<Particle> neighbours
    {
        get;set;
    }
    public int hashId //es el hash de nuestro diccionario espacial, lo necesitamos para saber donde nos encontramos y no calcular cada iteracion el hash de nuestra partícula
    {
        get;set;
    }
    private Vector3 gravity;
// Start is called before the first frame update
void Awake()
    {
        this.h = ParticleEmitter.distanceBetweenParticles;
        //GameObject.FindGameObjectsWithTag("ParticleEmitter");
        this.position = this.transform.position;
        this.neighbours = new List<Particle>();
    }

    public float BorderFix(float pos,float limit_1,float limit_2, String axis)
    {
        if (pos < limit_1)
        {
            pos = limit_1;
            /* if (axis.Equals("x"))
             {
                 this.velocity.x = 0;

             }
             else if (axis.Equals("y"))
             {
                 this.velocity.y = 0;

             }
             else if (axis.Equals("z"))
             {
                 this.velocity.z = 0;
             }*/
            this.velocity = this.velocity.normalized * -0.3f;
        }
        else if (pos > limit_2)
        {
           pos = limit_2;
            /* if (axis.Equals("x"))
             {
                 this.velocity.x = 0;

             }
             else if (axis.Equals("y"))
             {
                 this.velocity.y = 0;

             }
             else if (axis.Equals("z"))
             {
                 this.velocity.z = 0;
             }*/
            this.velocity = this.velocity.normalized * -0.3f;
        }



            return pos;
    }

    public void moveParticle()
    {
        this.neighbours= ParticleEmitter.findMyNeighbourInHash(this);
        this.position = transform.position;
        this.gravity = ParticleEmitter.gravity;
        acceleration = gravity;
        calculateDensity();

        //Then we got 3 different forces we need to calculate,  fex + fv + fp being gravity,viscosity and pressure respectively

        //First fpi
        Vector3 fpi = new Vector3(0, 0, 0);
        //fpi = calculate_fpi(particle);
        fpi = calculate_fpiLooking4Stability();

        //Then fv
        Vector3 fv = new Vector3(0, 0, 0);
        fv = calculate_fv();
        //Finally we get the whole aceleration

        Vector3 acceleration_pressure = fpi / this.mass;
        Vector3 acceleration_viscosity = fv / this.mass;

        this.acceleration = this.gravity + 4 * acceleration_pressure + 2 * acceleration_viscosity;
        if (this.acceleration.magnitude>10)
        {
            this.acceleration=this.acceleration.normalized * 10;
        }
        //x=x0+v0t+12at2
        this.velocity += acceleration * Time.deltaTime;
        position += velocity * Time.deltaTime + 0.5f * acceleration * Mathf.Pow(Time.deltaTime, 2);

        position.x = BorderFix(position.x, ParticleEmitter.limit_x[0], ParticleEmitter.limit_x[1], "x");
        position.y = BorderFix(position.y, ParticleEmitter.limit_y[0], ParticleEmitter.limit_y[1], "y");
        position.z = BorderFix(position.z, ParticleEmitter.limit_z[0], ParticleEmitter.limit_z[1], "z");

        this.transform.position = this.position;
    }

    public void calculateDensity()
    {
        float smooth_norm = 15 / (Mathf.PI * h * h * h);
        float sumPi = smooth_norm;
        for (int i = 0; i < neighbours.Count; i++)
        {
            float distance = (this.transform.position - neighbours[i].transform.position).magnitude;
            if (distance.Equals(0f))
            {
                distance = 0.01f;
            }
            float spiky_smoothing_kernel = smooth_norm * Mathf.Pow((1 - distance) / h, 3);
            sumPi += neighbours[i].mass * spiky_smoothing_kernel;
        }

        this.density = sumPi;
    }

    public Vector3 calculate_fpi()
    {
        Vector3 fpi_final = new Vector3(0, 0, 0);
        for (int i = 0; i < this.neighbours.Count; i++)
        {
            Vector3 distance = this.transform.position - this.neighbours[i].transform.position;
            if (distance.Equals(new Vector3(0, 0, 0)))
            {
                distance = new Vector3(0.01f, 0.01f, 0.01f);
            }
            float pj = gas_constant * this.neighbours[i].density;
            if (pj.Equals(0))
            {
                pj = 0.001f;
            }
            Vector3 kernel = (45 / (Mathf.PI * h * h * h * h)) * Mathf.Pow(1 - distance.magnitude / h, 2) * (distance / distance.magnitude);
            fpi_final += (this.neighbours[i].mass / pj) * ((this.density + pj) / 2) * kernel;
        }

        return -fpi_final * (this.mass / this.density);
    }

    public Vector3 calculate_fpiLooking4Stability()
    {   //Constant that we must set between 1000 and 3600 being 10 the minimun possible and something to take in care just if necessary 
        int K = 10;
        Vector3 fpi_final = new Vector3(0, 0, 0);
        for (int i = 0; i < this.neighbours.Count; i++)
        {
            Vector3 distance = this.transform.position - this.neighbours[i].transform.position;
            if (distance.Equals(new Vector3(0,0,0)))
            {
                distance = new Vector3(0.01f,0.01f,0.01f);
            }
            fpi_final += K * ((h - distance.magnitude) / distance.magnitude) * distance;
        }

        return fpi_final;
    }

    public Vector3 calculate_fv()
    {
        Vector3 fv_final = new Vector3(0, 0, 0);
        foreach(Particle p in neighbours)
        {
            Vector3 distance = this.transform.position - p.transform.position;
            if (distance.Equals(new Vector3(0, 0, 0)))
            {
                distance = new Vector3(0.001f, 0.001f, 0.0001f);
            }

            Vector3 velocity = p.velocity - this.velocity;
            float pj = gas_constant * p.density;

            float kernel = (45 / (Mathf.PI * Mathf.Pow(h, 6))) * (h - distance.magnitude);
            fv_final += (p.mass / pj) * velocity * kernel;
        }
        return fv_final * viscosity_constant * (this.mass / this.density);
    }
}
