"""
Traffic Light Collection Airflow DAG

This DAG orchestrates the traffic light data collection process:
1. Triggers the collection of traffic light data
2. Periodically checks the status of the collection
3. Sets timeout for commands that run too long
4. Generates a job summary and sends notifications when the job completes
"""

from datetime import datetime, timedelta
import json
import requests
from airflow import DAG
from airflow.models import Variable
from airflow.operators.python import PythonOperator
from airflow.operators.empty import EmptyOperator
from airflow.sensors.http_sensor import HttpSensor
from airflow.exceptions import AirflowException

# Default arguments for the DAG
default_args = {
    'owner': 'traffic_data_team',
    'depends_on_past': False,
    'email': ['traffic_alerts@vietmap.com'],
    'email_on_failure': True,
    'email_on_retry': False,
    'retries': 1,
    'retry_delay': timedelta(minutes=5),
    'execution_timeout': timedelta(hours=3),
}

# API configuration
API_BASE_URL = Variable.get("api_base_url", "http://traffic-data-api:5000")
API_VERSION = "v1"
API_TIMEOUT = 30  # seconds


# Function to trigger the light collection process
def trigger_collect_lights(**context):
    dag_id = context['dag'].dag_id
    dag_run_id = context['run_id']

    url = f"{API_BASE_URL}/{API_VERSION}/scheduler/trigger-collect-lights"

    payload = {
        "airflow_dag_id": dag_id,
        "airflow_dag_run_id": dag_run_id
    }

    response = requests.post(
        url=url,
        json=payload,
        timeout=API_TIMEOUT
    )

    if response.status_code != 200:
        raise AirflowException(f"Failed to trigger light collection: {response.text}")

    result = response.json()

    if result.get('code') != 0:
        raise AirflowException(f"API returned error: {result.get('msg')}")

    context['ti'].xcom_push(key='dag_run_id', value=result['data']['dag_run_id'])

    print(f"Successfully triggered light collection with dag_run_id: {result['data']['dag_run_id']}")
    return result['data']['dag_run_id']


# Function to check if the collection is complete
def check_collect_lights_status(**context):
    dag_id = context['dag'].dag_id
    dag_run_id = context['run_id']

    url = f"{API_BASE_URL}/{API_VERSION}/scheduler/check-collect-lights"

    params = {
        "airflow_dag_id": dag_id,
        "airflow_dag_run_id": dag_run_id
    }

    response = requests.get(
        url=url,
        params=params,
        timeout=API_TIMEOUT
    )

    if response.status_code != 200:
        raise AirflowException(f"Failed to check light collection status: {response.text}")

    result = response.json()

    if result.get('code') != 0:
        raise AirflowException(f"API returned error: {result.get('msg')}")

    is_completed = result['data']['is_completed']
    status = result['data']['status']

    print(f"Collection status: {status}, is_completed: {is_completed}")

    if is_completed:
        context['ti'].xcom_push(key='collection_status', value='success')
        return True
    else:
        return False


# Function to set timeout for pending commands
def set_timeout_for_pending_commands(**context):
    dag_id = context['dag'].dag_id
    dag_run_id = context['run_id']

    url = f"{API_BASE_URL}/{API_VERSION}/scheduler/set-time-out-collect-lights"

    payload = {
        "airflow_dag_id": dag_id,
        "airflow_dag_run_id": dag_run_id
    }

    response = requests.post(
        url=url,
        json=payload,
        timeout=API_TIMEOUT
    )

    if response.status_code != 200:
        raise AirflowException(f"Failed to set timeout for pending commands: {response.text}")

    result = response.json()

    if result.get('code') != 0:
        raise AirflowException(f"API returned error: {result.get('msg')}")

    timedout_commands_count = result['data']['timedout_commands_count']
    dag_status_updated = result['data']['dag_status_updated']

    print(f"Set {timedout_commands_count} commands to timeout status. DAG status updated: {dag_status_updated}")

    # Store information for use in the summary
    context['ti'].xcom_push(key='timedout_commands_count', value=timedout_commands_count)
    context['ti'].xcom_push(key='collection_status', value='timeout')

    return result['data']


# Function to update the DAG run status
def update_dag_run_status(status, reason=None, **context):
    dag_id = context['dag'].dag_id
    dag_run_id = context['run_id']

    url = f"{API_BASE_URL}/{API_VERSION}/scheduler/update-dag-run-status"

    payload = {
        "airflow_dag_id": dag_id,
        "airflow_dag_run_id": dag_run_id,
        "status": status
    }

    if reason:
        payload["reason"] = reason

    response = requests.post(
        url=url,
        json=payload,
        timeout=API_TIMEOUT
    )

    if response.status_code != 200:
        raise AirflowException(f"Failed to update DAG run status: {response.text}")

    result = response.json()

    if result.get('code') != 0:
        raise AirflowException(f"API returned error: {result.get('msg')}")

    print(f"Updated DAG run status to {status}")
    return result['data']


# Function to generate job summary and send notifications
def generate_summary_and_notify(**context):
    dag_id = context['dag'].dag_id
    dag_run_id = context['run_id']

    # Get email addresses for notification from DAG configuration
    email_addresses = default_args.get('email', [])

    # Add any additional notification recipients here
    teams_channel = "traffic_monitoring"

    # Combine all notification recipients
    notify_to = email_addresses + [teams_channel]

    url = f"{API_BASE_URL}/{API_VERSION}/scheduler/summary-job-and-push-notify"

    payload = {
        "airflow_dag_id": dag_id,
        "airflow_dag_run_id": dag_run_id,
        "notify_to": notify_to
    }

    try:
        response = requests.post(
            url=url,
            json=payload,
            timeout=API_TIMEOUT
        )

        if response.status_code != 200:
            print(f"Warning: Failed to generate summary and send notifications: {response.text}")
            return False

        result = response.json()

        if result.get('code') != 0:
            print(f"Warning: API returned error: {result.get('msg')}")
            return False

        summary = result['data']

        print(f"Successfully generated job summary and sent notifications")
        print(f"Job status: {summary['status']}")
        print(f"Total traffic lights: {summary['total_lights']}")
        print(f"Processed traffic lights: {summary['processed_lights']}")
        print(f"Success rate: {summary['success_rate']}%")

        return True
    except Exception as e:
        print(f"Error generating summary and sending notifications: {str(e)}")
        return False


# Create the DAG
with DAG(
        'traffic_light_collection',
        default_args=default_args,
        description='Collect and process traffic light data',
        schedule_interval='0 0 * * *',  # Run daily at midnight
        start_date=datetime(2025, 3, 1),
        catchup=False,
        tags=['traffic', 'data_collection'],
) as dag:
    start = EmptyOperator(
        task_id='start',
        dag=dag,
    )

    trigger_collection = PythonOperator(
        task_id='trigger_collect_lights',
        python_callable=trigger_collect_lights,
        provide_context=True,
        dag=dag,
    )

    check_collection_status = HttpSensor(
        task_id='check_collection_status',
        http_conn_id='traffic_data_api',
        endpoint=f"{API_VERSION}/scheduler/check-collect-lights",
        request_params={
            'airflow_dag_id': '{{ dag.dag_id }}',
            'airflow_dag_run_id': '{{ run_id }}'
        },
        response_check=lambda response: json.loads(response.text)['data']['is_completed'] == True,
        poke_interval=60,  # Check every 60 seconds
        timeout=7200,  # Timeout after 2 hours
        mode='reschedule',  # Release worker slot while waiting
        soft_fail=True,  # Continue the DAG even if this task fails
        dag=dag,
    )

    handle_timeout = PythonOperator(
        task_id='handle_timeout',
        python_callable=set_timeout_for_pending_commands,
        provide_context=True,
        trigger_rule='all_failed',  # Execute if check_collection_status fails/times out
        dag=dag,
    )

    update_success_status = PythonOperator(
        task_id='update_success_status',
        python_callable=update_dag_run_status,
        op_kwargs={'status': 'Success', 'reason': 'Collection completed successfully'},
        provide_context=True,
        trigger_rule='all_success',  # Execute if check_collection_status succeeds
        dag=dag,
    )

    update_timeout_status = PythonOperator(
        task_id='update_timeout_status',
        python_callable=update_dag_run_status,
        op_kwargs={'status': 'Timeout', 'reason': 'Collection timed out'},
        provide_context=True,
        trigger_rule='all_done',  # Execute after handle_timeout is done
        dag=dag,
    )

    generate_summary = PythonOperator(
        task_id='generate_summary_and_notify',
        python_callable=generate_summary_and_notify,
        provide_context=True,
        trigger_rule='all_done',  # Execute regardless of upstream task status
        dag=dag,
    )

    end = EmptyOperator(
        task_id='end',
        dag=dag,
    )

    # Define task dependencies
    start >> trigger_collection >> check_collection_status
    check_collection_status >> update_success_status
    check_collection_status >> handle_timeout >> update_timeout_status
    update_success_status >> generate_summary
    update_timeout_status >> generate_summary
    generate_summary >> end