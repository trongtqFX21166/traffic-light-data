from airflow import DAG
from airflow.operators.python import PythonOperator
from airflow.operators.bash import BashOperator
from airflow.utils.dates import days_ago
from datetime import timedelta
import requests
import json
import time

# API endpoints
API_BASE_URL = "http://192.168.8.230:31215"
TRIGGER_URL = f"{API_BASE_URL}/v1/scheduler/trigger-collect-lights"
CHECK_URL = f"{API_BASE_URL}/v1/scheduler/check-collect-lights"
TIMEOUT_URL = f"{API_BASE_URL}/v1/scheduler/set-time-out-collect-lights"
LOAD_CACHE_URL = f"{API_BASE_URL}/v1/cache/load-traffic-lights"

default_args = {
    'owner': 'airflow',
    'depends_on_past': False,
    'start_date': days_ago(1),
    'email_on_failure': True,
    'email_on_retry': False,
    'retries': 1,
    'retry_delay': timedelta(minutes=5),
    'execution_timeout': timedelta(minutes=15),  # Set execution timeout to 15 minutes
    'dagrun_timeout': timedelta(minutes=15)  # Set DAG run timeout to 15 minutes
}

dag = DAG(
    'traffic_light_collection',
    default_args=default_args,
    description='Traffic Light Data Collection Pipeline',
    schedule_interval=timedelta(minutes=30),  # Run every 30 minutes
    catchup=False,
)


def trigger_collect_lights(**context):
    """Trigger the light collection process"""
    dag_id = context['dag'].dag_id
    dag_run_id = context['run_id']

    # Check if this is a manually triggered run from Airflow UI
    is_manual_trigger = context.get('dag_run').external_trigger
    if is_manual_trigger:
        print(f"This is a manually triggered DAG run with run_id: {dag_run_id}")

    payload = {
        "airflow_dag_id": dag_id,
        "airflow_dag_run_id": dag_run_id
    }

    response = requests.post(TRIGGER_URL, json=payload)
    response.raise_for_status()  # Raise exception for HTTP errors

    result = response.json()
    # Extract dag_run_id from the response data structure based on the API pattern
    dag_run_id = result.get('data', {}).get('dag_run_id')
    context['ti'].xcom_push(key='dag_run_id', value=dag_run_id)
    context['ti'].xcom_push(key='collection_success', value=False)  # Initialize as False
    context['ti'].xcom_push(key='is_manual_trigger', value=is_manual_trigger)  # Store the trigger type

    print(f"Triggered light collection with DAG run ID: {dag_run_id}")
    return result


def check_collect_lights(**context):
    """Check the status of light collection with 1 hour timeout"""
    dag_id = context['dag'].dag_id
    dag_run_id = context['run_id']

    params = {
        "airflow_dag_id": dag_id,
        "airflow_dag_run_id": dag_run_id
    }

    # 15 attempts × 60 seconds = 15 minutes total
    max_attempts = 5
    delay_seconds = 60  # Check every minute

    for attempt in range(max_attempts):
        response = requests.get(CHECK_URL, params=params)
        response.raise_for_status()

        result = response.json()
        print(f"Check attempt {attempt + 1}: {result}")

        # Based on IOTHubResponse structure in the repository
        if result.get('code') == 0 and result.get('data', {}).get('is_completed', False):
            print("All traffic light commands completed successfully")
            context['ti'].xcom_push(key='collection_success', value=True)
            return True

        # Sleep before next attempt
        time.sleep(delay_seconds)

    # If we reached here, collection didn't complete within the timeout
    print("Traffic light collection did not complete within the allotted time (15 minutes)")
    context['ti'].xcom_push(key='collection_success', value=False)
    return False


def update_collection_status(**context):
    """Update the collection status based on check result"""
    dag_id = context['dag'].dag_id
    dag_run_id = context['run_id']
    collection_success = context['ti'].xcom_pull(key='collection_success')
    is_manual_trigger = context['ti'].xcom_pull(key='is_manual_trigger', default=False)

    # Add information about trigger type to the payload
    status_payload = {
        "airflow_dag_id": dag_id,
        "airflow_dag_run_id": dag_run_id,
        "is_manual_trigger": is_manual_trigger
    }

    # If collection was successful, update the status to "Done" and return
    if collection_success:
        print(f"Traffic light collection completed successfully at {time.strftime('%Y-%m-%d %H:%M:%S')}")
        try:
            status_payload["status"] = "Done"

            # Call API to update the status to Done
            response = requests.post(f"{API_BASE_URL}/v1/scheduler/update-dag-run-status", json=status_payload)
            response.raise_for_status()

            result = response.json()
            print(f"DAG run status updated to Done: {result}")
            return "Collection completed successfully, status updated to Done"
        except Exception as e:
            print(f"Error updating DAG run status to Done: {str(e)}")
            # Continue even if the update fails
            return "Collection completed successfully, but status update failed"

    # If we got here, the collection timed out - call the timeout endpoint
    print(f"Setting collection timeout status at {time.strftime('%Y-%m-%d %H:%M:%S')}")

    try:
        status_payload["status"] = "Timeout"

        response = requests.post(TIMEOUT_URL, json=status_payload)
        response.raise_for_status()

        result = response.json()
        print(f"Timeout status set: {result}")
        return "Collection timed out, status updated"
    except Exception as e:
        print(f"Error setting timeout status: {str(e)}")
        raise


def load_traffic_lights_cache(**context):
    """Load traffic lights data into Redis cache"""
    try:
        print(f"Loading traffic lights into cache at {time.strftime('%Y-%m-%d %H:%M:%S')}")
        response = requests.post(LOAD_CACHE_URL)
        response.raise_for_status()

        result = response.json()
        print(f"Cache loading result: {result}")

        if result.get('code') == 0:
            return "Traffic lights cache loaded successfully"
        else:
            raise Exception(f"Failed to load cache: {result.get('msg', 'Unknown error')}")
    except Exception as e:
        print(f"Error loading traffic lights cache: {str(e)}")
        raise


# Define tasks
trigger_task = PythonOperator(
    task_id='trigger_collect_lights',
    python_callable=trigger_collect_lights,
    provide_context=True,
    dag=dag,
)

check_task = PythonOperator(
    task_id='check_collect_lights',
    python_callable=check_collect_lights,
    provide_context=True,
    dag=dag,
)

update_status_task = PythonOperator(
    task_id='update_collect_status',
    python_callable=update_collection_status,
    provide_context=True,
    dag=dag,
)

load_cache_task = PythonOperator(
    task_id='load_traffic_lights_cache',
    python_callable=load_traffic_lights_cache,
    provide_context=True,
    dag=dag,
)

# Define the task dependencies
trigger_task >> check_task >> update_status_task >> load_cache_task